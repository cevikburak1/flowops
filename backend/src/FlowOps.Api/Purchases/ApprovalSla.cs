using System.Security.Claims;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;

namespace FlowOps.Api;

internal sealed record ApprovalSlaPolicy(BusinessCalendar Calendar, int BusinessMinutes, int? WarningMinutes, string Action = "administrator",
    string? Fallback = null, int? WorkflowMinutes = null)
{
    public bool Valid() => Calendar?.Valid() == true && BusinessMinutes is >= 1 and <= 14400 &&
        (WarningMinutes is null || WarningMinutes > 0 && WarningMinutes < BusinessMinutes) &&
        Action is "administrator" or "supervisor" or "department" or "mark" or "reassign" &&
        (Action == "reassign" ? Fallback is { Length: >= 1 and <= 255 } : Fallback is null) &&
        WorkflowMinutes is null or (>= 1 and <= 14400);
}

internal static class ApprovalSla
{
    public static void Map(RouteGroupBuilder tenant)
    {
        tenant.MapGet("/approval-sla", GetAsync);
        tenant.MapPost("/approval-sla", SaveAsync);
    }

    internal static ApprovalSlaPolicy? Read(string? json) => json is null ? null :
        JsonSerializer.Deserialize<ApprovalSlaPolicy>(json, JsonSerializerOptions.Web);

    internal static DateTimeOffset? WorkflowDeadline(string? policyJson, DateTimeOffset now) =>
        Read(policyJson) is { WorkflowMinutes: int minutes } policy ? policy.Calendar.Deadline(now, minutes) : null;

    internal static DateTimeOffset? ProcessTaskDeadline(string? policyJson, DateTimeOffset now, DateTimeOffset? workflowDue)
        => ProcessTaskSchedule(policyJson, now, workflowDue).DueAt;

    internal static (DateTimeOffset? DueAt, DateTimeOffset? WarnAt) ProcessTaskSchedule(
        string? policyJson, DateTimeOffset now, DateTimeOffset? workflowDue)
    {
        var policy = Read(policyJson);
        if (policy is null) return (workflowDue, null);
        var deadline = policy.Calendar.Deadline(now, policy.BusinessMinutes);
        var due = workflowDue is { } overall && overall < deadline ? overall : deadline;
        var warning = policy.WarningMinutes is int minutes ? policy.Calendar.Deadline(now, policy.BusinessMinutes - minutes) : (DateTimeOffset?)null;
        return (due, warning < due ? warning : null);
    }

    internal static void Schedule(Approval approval, string? policyJson, DateTimeOffset now, DateTimeOffset? workflowDue = null)
    {
        var policy = Read(policyJson);
        approval.DueAt = policy?.Calendar.Deadline(now, policy.BusinessMinutes) ?? now.AddHours(48);
        approval.WarnAt = policy?.WarningMinutes is int minutes ? policy.Calendar.Deadline(now, policy.BusinessMinutes - minutes) : null;
        if (workflowDue is { } deadline && deadline < approval.DueAt)
        {
            approval.DueAt = deadline;
            if (approval.WarnAt >= deadline) approval.WarnAt = null;
        }
        approval.WarnedAt = null;
        approval.EscalatedAt = null;
        approval.EscalationAction = policy?.Action;
        approval.EscalationFallback = policy?.Fallback;
    }

    private static async Task<IResult> GetAsync(Guid organizationId, ClaimsPrincipal user, FlowOpsDb db, CancellationToken token)
    {
        if (await OrganizationEndpoints.FindAdminAsync(db, organizationId, user, token) is null) return Results.NotFound();
        var org = await db.Organizations.SingleAsync(row => row.Id == organizationId, token);
        return Results.Ok(new { org.ApprovalSlaRevision, Policy = Read(org.ApprovalSlaJson) });
    }

    private static async Task<IResult> SaveAsync(Guid organizationId, SlaChange input, ClaimsPrincipal user, FlowOpsDb db, CancellationToken token)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(token);
        var org = await OrganizationEndpoints.LockAsync(db, organizationId, token);
        var actor = await OrganizationEndpoints.FindAdminAsync(db, organizationId, user, token);
        if (org is null || actor is null) return Results.NotFound();
        if (input.Revision != org.ApprovalSlaRevision) return Results.Conflict(new { Code = "slaChanged" });
        if (input.Policy is { } policy && (!policy.Valid() || policy.Fallback is string subject &&
            !await db.Memberships.AnyAsync(row => row.OrganizationId == organizationId && row.Subject == subject &&
                (PurchaseEndpoints.ManagerRoles.Contains(row.Role) || row.Role == "Approver"), token)))
            return Results.BadRequest(new { Code = "invalidSla" });
        org.ApprovalSlaJson = input.Policy is null ? null : JsonSerializer.Serialize(input.Policy, JsonSerializerOptions.Web);
        org.ApprovalSlaRevision++;
        db.OrganizationEvents.Add(new() { OrganizationId = organizationId, Actor = actor.Subject, Action = "approvalSlaUpdated" });
        await db.SaveChangesAsync(token);
        await transaction.CommitAsync(token);
        return Results.Ok(new { org.ApprovalSlaRevision });
    }

    internal sealed record SlaChange(int Revision, ApprovalSlaPolicy? Policy);
}
