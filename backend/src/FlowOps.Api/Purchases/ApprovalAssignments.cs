using System.Security.Claims;
using Microsoft.EntityFrameworkCore;

namespace FlowOps.Api;

internal sealed class ApprovalAssignments(IServiceScopeFactory scopes, ILogger<ApprovalAssignments> logger) : BackgroundService
{
    public static void Map(RouteGroupBuilder tenant)
    {
        tenant.MapPost("/purchases/{purchaseId:guid}/approvals/{stage:int}/assign", AssignAsync);
        tenant.MapPost("/purchases/{purchaseId:guid}/approvals/{stage:int}/delegate", DelegateAsync);
    }

    public static void Record(FlowOpsDb db, Approval approval, string actor) =>
        db.ApprovalAssignments.Add(new ApprovalAssignment { OrganizationId = approval.OrganizationId,
            PurchaseId = approval.PurchaseId, Stage = approval.Stage, Assignee = approval.Assignee, Actor = actor });

    public static string[] EligibleRoles(int stage) => stage == 1 ? PurchaseEndpoints.ManagerRoles : ["Approver"];

    private static Task<IResult> AssignAsync(Guid organizationId, Guid purchaseId, int stage, AssignInput input,
        ClaimsPrincipal user, FlowOpsDb db, CancellationToken token) =>
        TransferAsync(organizationId, purchaseId, stage, input, user, db, false, token);

    private static Task<IResult> DelegateAsync(Guid organizationId, Guid purchaseId, int stage, AssignInput input,
        ClaimsPrincipal user, FlowOpsDb db, CancellationToken token) =>
        TransferAsync(organizationId, purchaseId, stage, input, user, db, true, token);

    internal static async Task<IResult> TransferAsync(Guid organizationId, Guid purchaseId, int stage, AssignInput input,
        ClaimsPrincipal user, FlowOpsDb db, bool delegation, CancellationToken token)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(token);
        var subject = PurchaseEndpoints.Subject(user);
        var organization = await OrganizationEndpoints.LockAsync(db, organizationId, token);
        if (organization is null) return Results.NotFound();
        var actor = await db.Memberships.FindAsync([organizationId, subject], token);
        if (actor is null || !delegation && !OrganizationEndpoints.CanManage(actor.Role)) return Results.NotFound();
        var purchase = await db.Purchases.FindAsync([organizationId, purchaseId], token);
        var approval = await db.Approvals.FindAsync([organizationId, purchaseId, stage], token);
        if (purchase is null || approval is null) return Results.NotFound();
        var roles = EligibleRoles(stage);
        var parallel = stage == 1 && purchase.ReviewersJson is not null;
        var previous = parallel ? input.ExpectedAssignee : approval.Assignee;
        var vote = parallel && previous is not null ? await db.ApprovalVotes.FindAsync([organizationId, purchaseId, stage, previous], token) : null;
        if (delegation && (previous != subject || !roles.Contains(actor.Role))) return Results.NotFound();
        if (approval.Decision is not null || (parallel ? vote is null || vote.Decision is not null :
            input.ExpectedAssignee != approval.Assignee && input.Assignee != approval.Assignee))
            return Results.Conflict(new { code = "assignmentChanged" });
        if (previous == input.Assignee) return Results.NoContent();
        var assignedToAnotherStage = await db.Approvals.AnyAsync(x => x.OrganizationId == organizationId &&
            x.PurchaseId == purchaseId && x.Stage != stage && x.Assignee == input.Assignee, token);
        if (input.Assignee is null || input.Assignee == purchase.Requester || stage == 1 && input.Assignee == purchase.Finance ||
            assignedToAnotherStage || (parallel && await db.ApprovalVotes.AnyAsync(x => x.OrganizationId == organizationId &&
                x.PurchaseId == purchaseId && x.Stage == stage && x.Subject == input.Assignee, token)) ||
            !await db.Memberships.AnyAsync(x => x.OrganizationId == organizationId &&
                x.Subject == input.Assignee && roles.Contains(x.Role), token))
            return Results.BadRequest(new { code = "invalidAssignee" });
        if (vote is not null)
        {
            db.ApprovalVotes.Remove(vote);
            db.ApprovalVotes.Add(new() { OrganizationId = organizationId, PurchaseId = purchaseId, Stage = stage, Subject = input.Assignee });
        }
        if (approval.Assignee == previous) approval.Assignee = input.Assignee;
        if (!delegation && !parallel)
            ApprovalSla.Schedule(approval, organization.ApprovalSlaJson, DateTimeOffset.UtcNow, purchase.WorkflowDueAt);
        db.ApprovalAssignments.Add(new() { OrganizationId = organizationId, PurchaseId = purchaseId,
            Stage = stage, Assignee = input.Assignee, Actor = actor.Subject });
        Notify(db, approval, input.Assignee, "approvalAssigned");
        PurchaseEndpoints.Record(db, purchase, actor.Subject, delegation ? "approvalDelegated" : "approvalReassigned");
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
            try { await ProcessSla.EscalateAsync(scopes, token); }
            catch (Exception exception) when (!token.IsCancellationRequested)
            {
                logger.LogError(exception, "Process escalation failed");
            }
            try { await EscalateAsync(token); }
            catch (Exception exception) when (!token.IsCancellationRequested)
            {
                logger.LogError(exception, "Approval escalation failed");
            }
        }
    }

    internal async Task EscalateAsync(CancellationToken token)
    {
        await using var scope = scopes.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<FlowOpsDb>();
        await using var transaction = await db.Database.BeginTransactionAsync(token);
        var now = DateTimeOffset.UtcNow;
        var organization = await db.Organizations.FromSqlInterpolated($"""
            SELECT org.* FROM flowops."Organizations" org
            WHERE EXISTS (
                SELECT 1 FROM flowops."Approvals" approval WHERE approval."OrganizationId" = org."Id" AND
                    approval."Decision" IS NULL AND (
                        (approval."EscalatedAt" IS NULL AND approval."DueAt" <= {now}) OR
                        (approval."WarnedAt" IS NULL AND approval."WarnAt" <= {now} AND approval."DueAt" > {now})))
            ORDER BY org."Id" LIMIT 1 FOR UPDATE OF org SKIP LOCKED
            """).ToListAsync(token);
        if (organization.SingleOrDefault() is not { } org) return;
        var approvals = await db.Approvals.FromSqlInterpolated($"""
            SELECT * FROM flowops."Approvals"
            WHERE "OrganizationId" = {org.Id} AND "Decision" IS NULL AND (
                ("EscalatedAt" IS NULL AND "DueAt" <= {now}) OR
                ("WarnedAt" IS NULL AND "WarnAt" <= {now} AND "DueAt" > {now}))
            ORDER BY "DueAt" LIMIT 50 FOR UPDATE SKIP LOCKED
            """).ToListAsync(token);
        foreach (var approval in approvals)
        {
            if (approval.DueAt > now)
            {
                var pending = await db.ApprovalVotes.Where(vote => vote.OrganizationId == approval.OrganizationId &&
                    vote.PurchaseId == approval.PurchaseId && vote.Stage == approval.Stage && vote.Decision == null)
                    .Select(vote => vote.Subject).ToListAsync(token);
                if (pending.Count == 0) Notify(db, approval, approval.Assignee, "approvalReminder");
                else foreach (var reviewer in pending) Notify(db, approval, reviewer, "approvalReminder");
                approval.WarnedAt = now;
                RecordEscalation(db, approval, "approvalReminder", now);
                continue;
            }
            if (approval.EscalationAction == "reassign" && await TryFallbackAsync(db, approval, token))
            {
                approval.EscalatedAt = now;
                continue;
            }
            var recipients = db.Memberships.AsNoTracking().Where(x => x.OrganizationId == approval.OrganizationId &&
                x.Subject != approval.Assignee && (x.Role == "OrganizationOwner" || x.Role == "OrganizationAdmin"));
            var assignee = await db.Memberships.AsNoTracking().SingleOrDefaultAsync(x =>
                x.OrganizationId == approval.OrganizationId && x.Subject == approval.Assignee, token);
            var supervisor = assignee is null ? null : await OrganizationEndpoints.ResolveManagerAsync(db, assignee, token);
            var departmentLead = approval.EscalationAction == "department" && assignee?.DepartmentId is Guid department
                ? await db.OrganizationGroups.Where(group => group.OrganizationId == approval.OrganizationId && group.Id == department)
                    .Select(group => group.Lead).SingleOrDefaultAsync(token) : null;
            var recipient = await recipients.OrderBy(x => x.Subject != supervisor)
                .ThenBy(x => x.Role != "OrganizationOwner").ThenBy(x => x.Subject)
                .Select(x => x.Subject).FirstOrDefaultAsync(token);
            var target = approval.EscalationAction switch
            {
                "supervisor" => supervisor ?? recipient,
                "department" => departmentLead ?? recipient,
                _ => recipient
            };
            if (approval.EscalationAction != "mark")
                Notify(db, approval, target ?? approval.Assignee, "approvalOverdue");
            approval.EscalatedAt = now;
            RecordEscalation(db, approval, "approvalOverdue", now);
        }
        await db.SaveChangesAsync(token);
        await transaction.CommitAsync(token);
    }

    private static async Task<bool> TryFallbackAsync(FlowOpsDb db, Approval approval, CancellationToken token)
    {
        if (approval.EscalationFallback is not string fallback || fallback == approval.Assignee) return false;
        var purchase = await db.Purchases.FindAsync([approval.OrganizationId, approval.PurchaseId], token);
        if (purchase is null || purchase.Requester == fallback || approval.Stage == 1 &&
            (purchase.ReviewersJson is not null || purchase.Finance == fallback)) return false;
        var roles = EligibleRoles(approval.Stage);
        if (!await db.Memberships.AnyAsync(row => row.OrganizationId == approval.OrganizationId &&
            row.Subject == fallback && roles.Contains(row.Role), token) ||
            await db.Approvals.AnyAsync(row => row.OrganizationId == approval.OrganizationId && row.PurchaseId == approval.PurchaseId &&
                row.Stage != approval.Stage && row.Assignee == fallback, token)) return false;
        approval.Assignee = fallback;
        Record(db, approval, "system");
        Notify(db, approval, fallback, "approvalOverdue");
        PurchaseEndpoints.Record(db, purchase, "system", "approvalReassigned");
        return true;
    }

    private static void RecordEscalation(FlowOpsDb db, Approval approval, string action, DateTimeOffset now) =>
        db.Audit.Add(new() { OrganizationId = approval.OrganizationId, PurchaseId = approval.PurchaseId,
            Actor = "system", Action = action, CorrelationId = approval.PurchaseId.ToString(), At = now });

    internal sealed record AssignInput(string? Assignee, string? ExpectedAssignee);
}
