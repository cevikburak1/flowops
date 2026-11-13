using System.Security.Claims;
using Microsoft.EntityFrameworkCore;

namespace FlowOps.Api;

internal sealed class ApprovalAssignments(IServiceScopeFactory scopes, ILogger<ApprovalAssignments> logger) : BackgroundService
{
    public static void Map(RouteGroupBuilder tenant) =>
        tenant.MapPost("/purchases/{purchaseId:guid}/approvals/{stage:int}/assign", AssignAsync);

    public static void Record(FlowOpsDb db, Approval approval, string actor) =>
        db.ApprovalAssignments.Add(new ApprovalAssignment { OrganizationId = approval.OrganizationId,
            PurchaseId = approval.PurchaseId, Stage = approval.Stage, Assignee = approval.Assignee, Actor = actor });

    public static string[] EligibleRoles(int stage) => stage == 1 ? PurchaseEndpoints.ManagerRoles : ["Approver"];

    private static async Task<IResult> AssignAsync(Guid organizationId, Guid purchaseId, int stage, AssignInput input,
        ClaimsPrincipal user, FlowOpsDb db, CancellationToken token)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(token);
        if (await OrganizationEndpoints.LockAsync(db, organizationId, token) is null ||
            await OrganizationEndpoints.FindAdminAsync(db, organizationId, user, token) is not { } actor) return Results.NotFound();
        var purchase = await db.Purchases.FindAsync([organizationId, purchaseId], token);
        var approval = await db.Approvals.FindAsync([organizationId, purchaseId, stage], token);
        if (purchase is null || approval is null) return Results.NotFound();
        if (approval.Decision is not null || (input.ExpectedAssignee != approval.Assignee && input.Assignee != approval.Assignee))
            return Results.Conflict(new { code = "assignmentChanged" });
        if (approval.Assignee == input.Assignee) return Results.NoContent();
        var roles = EligibleRoles(stage);
        var alreadyApproved = await db.Approvals.AnyAsync(x => x.OrganizationId == organizationId &&
            x.PurchaseId == purchaseId && x.Stage != stage && x.Assignee == input.Assignee && x.Decision != null, token);
        if (input.Assignee is null || input.Assignee == purchase.Requester || input.Assignee == purchase.Finance ||
            alreadyApproved || !await db.Memberships.AnyAsync(x => x.OrganizationId == organizationId &&
                x.Subject == input.Assignee && roles.Contains(x.Role), token))
            return Results.BadRequest(new { code = "invalidAssignee" });
        approval.Assignee = input.Assignee;
        approval.DueAt = DateTimeOffset.UtcNow.AddHours(48);
        approval.EscalatedAt = null;
        Record(db, approval, actor.Subject);
        Notify(db, approval, approval.Assignee, "approvalAssigned");
        PurchaseEndpoints.Record(db, purchase, actor.Subject, "approvalReassigned");
        try
        {
            await db.SaveChangesAsync(token);
            await transaction.CommitAsync(token);
        }
        catch (DbUpdateConcurrencyException)
        {
            return Results.Conflict(new { code = "assignmentChanged" });
        }
        return Results.NoContent();
    }

    private static void Notify(FlowOpsDb db, Approval approval, string recipient, string kind) =>
        db.Notifications.Add(new Notification { OrganizationId = approval.OrganizationId,
            PurchaseId = approval.PurchaseId, Recipient = recipient, Kind = kind });

    protected override async Task ExecuteAsync(CancellationToken token)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(30));
        while (await timer.WaitForNextTickAsync(token))
        {
            try { await EscalateAsync(token); }
            catch (Exception exception) when (!token.IsCancellationRequested)
            {
                logger.LogError(exception, "Approval escalation failed");
            }
        }
    }

    private async Task EscalateAsync(CancellationToken token)
    {
        await using var scope = scopes.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<FlowOpsDb>();
        await using var transaction = await db.Database.BeginTransactionAsync(token);
        var now = DateTimeOffset.UtcNow;
        var approvals = await db.Approvals.FromSqlInterpolated($"""
            SELECT * FROM flowops."Approvals"
            WHERE "Decision" IS NULL AND "EscalatedAt" IS NULL AND "DueAt" <= {now}
            ORDER BY "DueAt" LIMIT 50 FOR UPDATE SKIP LOCKED
            """).ToListAsync(token);
        foreach (var approval in approvals)
        {
            var recipients = db.Memberships.AsNoTracking().Where(x => x.OrganizationId == approval.OrganizationId &&
                x.Subject != approval.Assignee && (x.Role == "OrganizationOwner" || x.Role == "OrganizationAdmin"));
            var assignee = await db.Memberships.AsNoTracking().SingleOrDefaultAsync(x =>
                x.OrganizationId == approval.OrganizationId && x.Subject == approval.Assignee, token);
            var supervisor = assignee is null ? null : await OrganizationEndpoints.ResolveManagerAsync(db, assignee, token);
            var recipient = await recipients.OrderBy(x => x.Subject != supervisor)
                .ThenBy(x => x.Role != "OrganizationOwner").ThenBy(x => x.Subject)
                .Select(x => x.Subject).FirstOrDefaultAsync(token);
            Notify(db, approval, recipient ?? approval.Assignee, "approvalOverdue");
            approval.EscalatedAt = now;
        }
        await db.SaveChangesAsync(token);
        await transaction.CommitAsync(token);
    }

    private sealed record AssignInput(string? Assignee, string? ExpectedAssignee);
}
