using System.Security.Claims;
using Microsoft.EntityFrameworkCore;

namespace FlowOps.Api;

internal sealed class ApprovalDelegation
{
    public Guid OrganizationId { get; init; }
    public required string Subject { get; init; }
    public required string Delegate { get; set; }
    public DateTimeOffset StartsAt { get; set; }
    public DateTimeOffset EndsAt { get; set; }
}

internal static class ApprovalDelegations
{
    public static void Map(RouteGroupBuilder tenant)
    {
        tenant.MapGet("/approval-delegation", GetAsync);
        tenant.MapPost("/approval-delegation", SaveAsync);
    }

    private static async Task<IResult> GetAsync(Guid organizationId, ClaimsPrincipal user, FlowOpsDb db, CancellationToken token)
    {
        var subject = PurchaseEndpoints.Subject(user);
        var member = await db.Memberships.FindAsync([organizationId, subject], token);
        if (member is null) return Results.NotFound();
        var delegation = await db.ApprovalDelegations.FindAsync([organizationId, subject], token);
        var roles = member.Role == "Approver" ? ApprovalAssignments.EligibleRoles(2) : ApprovalAssignments.EligibleRoles(1);
        var candidates = await db.Memberships.Where(row => row.OrganizationId == organizationId && row.Subject != subject &&
            roles.Contains(member.Role) && roles.Contains(row.Role))
            .OrderBy(row => row.Email).ThenBy(row => row.Subject).Select(row => new { row.Subject, row.Email }).ToListAsync(token);
        return Results.Ok(new { Delegation = delegation, Candidates = candidates });
    }

    private static async Task<IResult> SaveAsync(Guid organizationId, DelegationInput input, ClaimsPrincipal user, FlowOpsDb db,
        CancellationToken token)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(token);
        var subject = PurchaseEndpoints.Subject(user);
        if (await OrganizationEndpoints.LockAsync(db, organizationId, token) is null) return Results.NotFound();
        var member = await db.Memberships.FindAsync([organizationId, subject], token);
        if (member is null) return Results.NotFound();
        var current = await db.ApprovalDelegations.FindAsync([organizationId, subject], token);
        if (input.Delegate is null)
        {
            if (current is null) return Results.NoContent();
            db.ApprovalDelegations.Remove(current);
        }
        else
        {
            var roles = member.Role == "Approver" ? ApprovalAssignments.EligibleRoles(2) : ApprovalAssignments.EligibleRoles(1);
            if (!roles.Contains(member.Role) || input.Delegate == subject ||
                input.StartsAt is not { } start || input.EndsAt is not { } end ||
                end <= DateTimeOffset.UtcNow || end <= start || end - start > TimeSpan.FromDays(90) ||
                !await db.Memberships.AnyAsync(row => row.OrganizationId == organizationId && row.Subject == input.Delegate &&
                    roles.Contains(row.Role), token))
                return Results.BadRequest(new { code = "invalidDelegation" });
            if (current is null)
                db.ApprovalDelegations.Add(new() { OrganizationId = organizationId, Subject = subject, Delegate = input.Delegate,
                    StartsAt = start.ToUniversalTime(), EndsAt = end.ToUniversalTime() });
            else
            {
                current.Delegate = input.Delegate;
                current.StartsAt = start.ToUniversalTime();
                current.EndsAt = end.ToUniversalTime();
            }
        }
        db.OrganizationEvents.Add(new() { OrganizationId = organizationId, Actor = subject,
            Action = input.Delegate is null ? "approvalDelegationRemoved" : "approvalDelegationScheduled", Target = input.Delegate });
        await db.SaveChangesAsync(token);
        await transaction.CommitAsync(token);
        return Results.NoContent();
    }

    internal static async Task<string> ResolveAsync(FlowOpsDb db, Purchase purchase, int stage, string assignee, CancellationToken token)
    {
        var now = DateTimeOffset.UtcNow;
        var org = purchase.OrganizationId;
        var roles = ApprovalAssignments.EligibleRoles(stage);
        var target = await db.ApprovalDelegations.Where(row => row.OrganizationId == org && row.Subject == assignee &&
            row.StartsAt <= now && row.EndsAt > now && row.Delegate != purchase.Requester &&
            (stage != 1 || row.Delegate != purchase.Finance) &&
            db.Memberships.Any(member => member.OrganizationId == org && member.Subject == row.Subject && roles.Contains(member.Role)) &&
            db.Memberships.Any(member => member.OrganizationId == org && member.Subject == row.Delegate && roles.Contains(member.Role)) &&
            !db.Approvals.Any(approval => approval.OrganizationId == org && approval.PurchaseId == purchase.Id &&
                approval.Stage != stage && approval.Assignee == row.Delegate) &&
            !db.ApprovalVotes.Any(vote => vote.OrganizationId == org && vote.PurchaseId == purchase.Id && vote.Subject == row.Delegate))
            .Select(row => row.Delegate).SingleOrDefaultAsync(token);
        return target ?? assignee;
    }

    internal static async Task<(string Original, string Reviewer)[]> ResolveParallelAsync(
        FlowOpsDb db, Purchase purchase, int stage, string[] reviewers, CancellationToken token)
    {
        var reserved = reviewers.ToHashSet(StringComparer.Ordinal);
        var resolved = new (string Original, string Reviewer)[reviewers.Length];
        for (var index = 0; index < reviewers.Length; index++)
        {
            var original = reviewers[index];
            var target = await ResolveAsync(db, purchase, stage, original, token);
            resolved[index] = (original, reserved.Add(target) ? target : original);
        }
        return resolved;
    }

    internal sealed record DelegationInput(string? Delegate, DateTimeOffset? StartsAt, DateTimeOffset? EndsAt);
}
