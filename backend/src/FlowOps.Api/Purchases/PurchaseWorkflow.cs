using Elsa.Common.Models;
using Elsa.Extensions;
using Elsa.Workflows;
using Elsa.Workflows.Activities;
using Elsa.Workflows.Models;
using Elsa.Workflows.Runtime;
using Microsoft.EntityFrameworkCore;

namespace FlowOps.Api;

public sealed class PurchaseApprovalWorkflow : WorkflowBase
{
    internal const string DefinitionId = "flowops-purchase-approval";
    internal const int EngineVersion = 1;

    protected override void Build(IWorkflowBuilder builder)
    {
        builder.WithDefinitionId(DefinitionId);
        builder.Root = new Sequence
        {
            Activities =
            {
                new WaitForPurchaseApproval { Stage = new(1) },
                new WaitForPurchaseApproval { Stage = new(2) }
            }
        };
    }
}

public sealed class WaitForPurchaseApproval : Activity
{
    public Input<int> Stage { get; set; } = new(1);

    protected override async ValueTask ExecuteAsync(ActivityExecutionContext context)
    {
        var stage = Stage.Get(context);
        if (stage == 2)
        {
            var (organizationId, purchaseId) = PurchaseWorkflowWorker.ParseInstanceId(context.WorkflowExecutionContext.Id);
            await using var scope = context.GetRequiredService<IServiceScopeFactory>().CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<FlowOpsDb>();
            var needsFinance = await db.Purchases.AnyAsync(x => x.OrganizationId == organizationId &&
                x.Id == purchaseId && x.Finance != null, context.CancellationToken);
            var approved = await db.Approvals.AnyAsync(x => x.OrganizationId == organizationId &&
                x.PurchaseId == purchaseId && x.Stage == 1 && x.Decision == "approved", context.CancellationToken);
            if (!needsFinance || !approved)
            {
                await context.CompleteActivityAsync();
                return;
            }
        }

        context.CreateBookmark(ResumeApprovalAsync, new Dictionary<string, string> { ["flowopsStage"] = stage.ToString() });
    }

    private ValueTask ResumeApprovalAsync(ActivityExecutionContext context) => context.CompleteActivityAsync();
}

internal sealed class PurchaseWorkflowWorker(IServiceScopeFactory scopes, ILogger<PurchaseWorkflowWorker> logger)
    : BackgroundService
{
    internal static string InstanceId(Guid organizationId, Guid purchaseId) => $"{organizationId:N}:{purchaseId:N}";

    internal static (Guid OrganizationId, Guid PurchaseId) ParseInstanceId(string id) =>
        (Guid.ParseExact(id[..32], "N"), Guid.ParseExact(id[33..], "N"));

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(1));
        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            try
            {
                await ProcessNextAsync(stoppingToken);
            }
            catch (Exception exception) when (!stoppingToken.IsCancellationRequested)
            {
                logger.LogError(exception, "Purchase workflow queue iteration failed");
            }
        }
    }

    private async Task ProcessNextAsync(CancellationToken token)
    {
        await using var scope = scopes.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<FlowOpsDb>();
        await using var transaction = await db.Database.BeginTransactionAsync(token);
        var now = DateTimeOffset.UtcNow;
        var commands = await db.WorkflowCommands.FromSqlInterpolated($"""
            SELECT * FROM flowops."WorkflowCommands"
            WHERE "CompletedAt" IS NULL AND "NextAttemptAt" <= {now}
            ORDER BY "NextAttemptAt", "Id" LIMIT 1 FOR UPDATE SKIP LOCKED
            """).ToListAsync(token);
        var command = commands.SingleOrDefault();
        if (command is null) return;
        try
        {
            await AdvanceAsync(scope.ServiceProvider, db, command, token);
            command.CompletedAt = DateTimeOffset.UtcNow;
            await db.SaveChangesAsync(token);
            await transaction.CommitAsync(token);
        }
        catch (Exception exception) when (!token.IsCancellationRequested)
        {
            logger.LogError(exception, "Purchase workflow command {CommandId} attempt {Attempt} failed", command.Id, command.Attempts + 1);
            await transaction.RollbackAsync(token);
            db.ChangeTracker.Clear();
            await db.WorkflowCommands.Where(x => x.Id == command.Id && x.CompletedAt == null && x.Attempts == command.Attempts)
                .ExecuteUpdateAsync(set => set
                    .SetProperty(x => x.Attempts, x => x.Attempts + 1)
                    .SetProperty(x => x.NextAttemptAt, now.AddSeconds(Math.Min(120, 1 << Math.Min(command.Attempts, 7)))), token);
        }
    }

    private static async Task AdvanceAsync(IServiceProvider services, FlowOpsDb db, WorkflowCommand command, CancellationToken token)
    {
        var purchase = await db.Purchases.SingleAsync(x => x.OrganizationId == command.OrganizationId && x.Id == command.PurchaseId, token);
        var instanceId = InstanceId(purchase.OrganizationId, purchase.Id);
        var client = await services.GetRequiredService<IWorkflowRuntime>().CreateClientAsync(instanceId, token);
        if (!await client.InstanceExistsAsync(token))
        {
            if (command.Stage != 0) throw new InvalidOperationException("Expected an existing workflow instance.");
            var snapshot = purchase.WorkflowId is null ? null : await db.PurchaseWorkflowVersions.SingleAsync(x =>
                x.OrganizationId == purchase.OrganizationId && x.WorkflowId == purchase.WorkflowId && x.Number == purchase.WorkflowVersion, token);
            var handle = snapshot?.ElsaDefinitionId is string definitionId
                ? WorkflowDefinitionHandle.ByDefinitionVersionId(definitionId)
                : WorkflowDefinitionHandle.ByDefinitionId(PurchaseApprovalWorkflow.DefinitionId,
                    snapshot is null ? VersionOptions.Published : VersionOptions.SpecificVersion(snapshot.ElsaVersion));
            await client.CreateAndRunInstanceAsync(new()
            {
                WorkflowDefinitionHandle = handle,
                CorrelationId = instanceId
            }, token);
        }
        else if (command.Stage != 0)
        {
            var approval = await db.Approvals.SingleAsync(x => x.OrganizationId == purchase.OrganizationId &&
                x.PurchaseId == purchase.Id && x.Stage == command.Stage, token);
            var state = await client.ExportStateAsync(token);
            if (state.Bookmarks.Any(x => x.Id == approval.BookmarkId))
                await client.RunInstanceAsync(new() { BookmarkId = approval.BookmarkId }, token);
        }

        var progress = await client.ExportStateAsync(token);
        if (progress.SubStatus == WorkflowSubStatus.Suspended)
        {
            var bookmark = progress.Bookmarks.Single();
            var stage = int.Parse(bookmark.Metadata!["flowopsStage"]);
            if (stage <= command.Stage) throw new InvalidOperationException("Elsa did not consume the approval bookmark.");
            var assignee = stage == 1 ? purchase.Manager : purchase.Finance!;
            var approval = new Approval
            {
                OrganizationId = purchase.OrganizationId, PurchaseId = purchase.Id,
                Stage = stage, Assignee = assignee, BookmarkId = bookmark.Id, DueAt = DateTimeOffset.UtcNow.AddHours(48)
            };
            db.Approvals.Add(approval);
            ApprovalAssignments.Record(db, approval, "system");
            purchase.Status = stage == 1 ? "managerReview" : "financeReview";
            db.Notifications.Add(new Notification
            {
                OrganizationId = purchase.OrganizationId, PurchaseId = purchase.Id,
                Recipient = assignee, Kind = "approvalAssigned"
            });
            PurchaseEndpoints.Record(db, purchase, "system", purchase.Status);
            return;
        }

        if (progress.SubStatus != WorkflowSubStatus.Finished || command.Stage == 0)
            throw new InvalidOperationException($"Unexpected Elsa workflow state {progress.SubStatus}.");

        var decision = await db.Approvals.Where(x => x.OrganizationId == purchase.OrganizationId &&
            x.PurchaseId == purchase.Id && x.Stage == command.Stage).Select(x => x.Decision).SingleAsync(token);
        purchase.Status = decision ?? throw new InvalidOperationException("Missing approval decision.");
        db.Notifications.Add(new Notification
        {
            OrganizationId = purchase.OrganizationId, PurchaseId = purchase.Id,
            Recipient = purchase.Requester, Kind = purchase.Status
        });
        PurchaseEndpoints.Record(db, purchase, "system", purchase.Status);
    }
}
