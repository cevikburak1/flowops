using System.Net.Mail;
using System.Security.Claims;
using System.Security.Cryptography;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.EntityFrameworkCore;

namespace FlowOps.Api;

internal static class OrganizationEndpoints
{
    private static readonly string[] AssignableRoles = ["OrganizationAdmin", "Manager", "Approver", "Employee", "Viewer"];

    public static void Map(RouteGroupBuilder api, RouteGroupBuilder tenant)
    {
        api.MapPost("/invitations/accept", AcceptAsync);
        tenant.MapGet("/settings", GetAsync);
        tenant.MapPost("/invitations", InviteAsync);
        tenant.MapPost("/invitations/{invitationId:guid}/revoke", RevokeAsync);
        tenant.MapPost("/members/{memberSubject}/settings", UpdateMemberAsync);
        tenant.MapPost("/routing", UpdateRoutingAsync);
    }

    internal static async Task<Organization?> LockAsync(FlowOpsDb db, Guid id, CancellationToken token) =>
        (await db.Organizations.FromSqlInterpolated($"""
            SELECT * FROM flowops."Organizations" WHERE "Id" = {id} FOR UPDATE
            """).ToListAsync(token)).SingleOrDefault();

    internal static string? VerifiedEmail(ClaimsPrincipal user)
    {
        var email = user.FindFirstValue("email");
        return bool.TryParse(user.FindFirstValue("email_verified"), out var verified) && verified
            ? NormalizeEmail(email)
            : null;
    }

    private static string? NormalizeEmail(string? value)
    {
        var address = value?.Trim();
        return address is { Length: > 0 and <= 254 } && MailAddress.TryCreate(address, out var parsed) &&
            parsed.Address == address ? address.ToLowerInvariant() : null;
    }

    private static bool CanManage(string? role) => role is "OrganizationOwner" or "OrganizationAdmin";

    private static Task<Membership?> FindMemberAsync(FlowOpsDb db, Guid org, string subject, CancellationToken token) =>
        db.Memberships.SingleOrDefaultAsync(x => x.OrganizationId == org && x.Subject == subject, token);

    private static async Task<Membership?> FindAdminAsync(FlowOpsDb db, Guid org, ClaimsPrincipal user, CancellationToken token)
    {
        var member = await FindMemberAsync(db, org, PurchaseEndpoints.Subject(user), token);
        return CanManage(member?.Role) ? member : null;
    }

    private static IResult Invalid(string code) => Results.BadRequest(new { code });
    private static IResult Conflict(string code) => Results.Conflict(new { code });

    private static async Task<IResult> GetAsync(Guid organizationId, ClaimsPrincipal user, FlowOpsDb db, CancellationToken token)
    {
        var actor = await FindAdminAsync(db, organizationId, user, token);
        if (actor is null) return Results.NotFound();
        var organization = await db.Organizations.AsNoTracking().SingleAsync(x => x.Id == organizationId, token);
        var members = await db.Memberships.AsNoTracking().Where(x => x.OrganizationId == organizationId)
            .OrderBy(x => x.Email).ThenBy(x => x.Subject).Select(x => new { x.Subject, x.Email, x.Role, x.Manager }).ToListAsync(token);
        var now = DateTimeOffset.UtcNow;
        var invitations = await db.Invitations.AsNoTracking()
            .Where(x => x.OrganizationId == organizationId && x.ExpiresAt > now && x.AcceptedAt == null && x.RevokedAt == null)
            .OrderByDescending(x => x.ExpiresAt).Select(x => new { x.Id, x.Email, x.Role, x.ExpiresAt }).ToListAsync(token);
        return Results.Ok(new { Members = members, Invitations = invitations, organization.FinanceThreshold,
            organization.FinanceApprover, CanAssignAdmin = actor.Role == "OrganizationOwner" });
    }

    private static async Task<IResult> InviteAsync(Guid organizationId, InviteInput input, ClaimsPrincipal user, FlowOpsDb db, CancellationToken token)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(token);
        var organization = await LockAsync(db, organizationId, token);
        var actor = await FindAdminAsync(db, organizationId, user, token);
        if (actor is null || organization is null) return Results.NotFound();
        var email = NormalizeEmail(input.Email);
        if (email is null) return Invalid("invalidEmail");
        if (!AssignableRoles.Contains(input.Role) || (input.Role == "OrganizationAdmin" && actor.Role != "OrganizationOwner"))
            return Invalid("invalidRole");
        if (await db.Memberships.AnyAsync(x => x.OrganizationId == organizationId && x.Email == email, token))
            return Conflict("alreadyMember");

        var now = DateTimeOffset.UtcNow;
        var active = await db.Invitations.Where(x => x.OrganizationId == organizationId && x.Email == email &&
            x.AcceptedAt == null && x.RevokedAt == null && x.ExpiresAt > now).ToListAsync(token);
        foreach (var previous in active) previous.RevokedAt = now;
        var raw = RandomNumberGenerator.GetBytes(32);
        var invitation = new Invitation { OrganizationId = organizationId, Email = email, Role = input.Role!,
            ExpiresAt = now.AddDays(7), TokenHash = Convert.ToHexString(SHA256.HashData(raw)) };
        db.Invitations.Add(invitation);
        Record(db, organizationId, actor.Subject, "invited", email);
        await db.SaveChangesAsync(token);
        await transaction.CommitAsync(token);
        return Results.Json(new { invitation.Id, Code = WebEncoders.Base64UrlEncode(raw), invitation.ExpiresAt }, statusCode: 201);
    }

    private static async Task<IResult> AcceptAsync(AcceptInput input, ClaimsPrincipal user, FlowOpsDb db, CancellationToken token)
    {
        var subject = PurchaseEndpoints.Subject(user);
        var email = VerifiedEmail(user);
        if (email is null) return Results.Json(new { code = "verifiedEmailRequired" }, statusCode: 403);
        if (input.Code is not { Length: 43 } || input.Code.Any(x => !char.IsAsciiLetterOrDigit(x) && x != '-' && x != '_'))
            return Invalid("invalidInvitation");
        var hash = Convert.ToHexString(SHA256.HashData(WebEncoders.Base64UrlDecode(input.Code)));
        var reference = await db.Invitations.AsNoTracking().Where(x => x.TokenHash == hash)
            .Select(x => new { x.Id, x.OrganizationId }).SingleOrDefaultAsync(token);
        if (reference is null) return Invalid("invalidInvitation");

        await using var transaction = await db.Database.BeginTransactionAsync(token);
        if (await LockAsync(db, reference.OrganizationId, token) is null) return Invalid("invalidInvitation");
        var invite = await db.Invitations.SingleAsync(x => x.OrganizationId == reference.OrganizationId && x.Id == reference.Id, token);
        if (invite.Email != email || invite.RevokedAt is not null || invite.ExpiresAt <= DateTimeOffset.UtcNow)
            return Invalid("invalidInvitation");
        var member = await FindMemberAsync(db, invite.OrganizationId, subject, token);
        if (invite.AcceptedAt is not null)
            return invite.AcceptedBy == subject && member is not null
                ? Results.Ok(new { invite.OrganizationId })
                : Invalid("invalidInvitation");

        if (member is null)
            db.Memberships.Add(new Membership { OrganizationId = invite.OrganizationId, Subject = subject, Email = email, Role = invite.Role });
        invite.AcceptedAt = DateTimeOffset.UtcNow;
        invite.AcceptedBy = subject;
        Record(db, invite.OrganizationId, subject, "invitationAccepted", email);
        await db.SaveChangesAsync(token);
        await transaction.CommitAsync(token);
        return Results.Ok(new { invite.OrganizationId });
    }

    private static async Task<IResult> RevokeAsync(Guid organizationId, Guid invitationId, ClaimsPrincipal user, FlowOpsDb db, CancellationToken token)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(token);
        var organization = await LockAsync(db, organizationId, token);
        var actor = await FindAdminAsync(db, organizationId, user, token);
        if (actor is null || organization is null) return Results.NotFound();
        var invitation = await db.Invitations.SingleOrDefaultAsync(x => x.OrganizationId == organizationId && x.Id == invitationId, token);
        if (invitation is null) return Results.NotFound();
        if (invitation.AcceptedAt is not null) return Conflict("alreadyAccepted");
        if (invitation.RevokedAt is null)
        {
            invitation.RevokedAt = DateTimeOffset.UtcNow;
            Record(db, organizationId, actor.Subject, "invitationRevoked", invitation.Email);
            await db.SaveChangesAsync(token);
            await transaction.CommitAsync(token);
        }
        return Results.NoContent();
    }

    private static async Task<IResult> UpdateMemberAsync(Guid organizationId, string memberSubject, MemberInput input,
        ClaimsPrincipal user, FlowOpsDb db, CancellationToken token)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(token);
        var organization = await LockAsync(db, organizationId, token);
        var actor = await FindAdminAsync(db, organizationId, user, token);
        if (actor is null || organization is null) return Results.NotFound();
        var member = await FindMemberAsync(db, organizationId, memberSubject, token);
        if (member is null) return Results.NotFound();
        var role = input.Role;
        if (member.Role == "OrganizationOwner" ? role != member.Role : !AssignableRoles.Contains(role))
            return Invalid("invalidRole");
        if (actor.Role != "OrganizationOwner" &&
            (member.Role == "OrganizationOwner" || role != member.Role && (member.Role == "OrganizationAdmin" || role == "OrganizationAdmin")))
            return Results.StatusCode(403);

        var manager = string.IsNullOrEmpty(input.Manager) ? null : input.Manager;
        if (manager is not null && (manager == member.Subject || !await db.Memberships.AnyAsync(x =>
            x.OrganizationId == organizationId && x.Subject == manager && PurchaseEndpoints.ManagerRoles.Contains(x.Role), token)))
            return Invalid("invalidManager");

        if (member.Role != role)
        {
            var losesManager = PurchaseEndpoints.ManagerRoles.Contains(member.Role) && !PurchaseEndpoints.ManagerRoles.Contains(role);
            var losesFinance = member.Role == "Approver" && role != "Approver";
            if ((losesManager && await db.Memberships.AnyAsync(x => x.OrganizationId == organizationId && x.Manager == member.Subject, token)) ||
                (losesFinance && organization.FinanceApprover == member.Subject) ||
                await db.Purchases.AnyAsync(x => x.OrganizationId == organizationId && x.Status != "approved" && x.Status != "rejected" &&
                    ((losesManager && x.Manager == member.Subject) || (losesFinance && x.Finance == member.Subject)), token))
                return Conflict("memberHasAssignments");
        }
        member.Role = role!;
        member.Manager = manager;
        Record(db, organizationId, actor.Subject, "memberUpdated", member.Subject);
        await db.SaveChangesAsync(token);
        await transaction.CommitAsync(token);
        return Results.NoContent();
    }

    private static async Task<IResult> UpdateRoutingAsync(Guid organizationId, RoutingInput input, ClaimsPrincipal user, FlowOpsDb db, CancellationToken token)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(token);
        var organization = await LockAsync(db, organizationId, token);
        var actor = await FindAdminAsync(db, organizationId, user, token);
        if (actor is null || organization is null) return Results.NotFound();
        if (input.Threshold is not decimal threshold || threshold is < 0m or > 999_999_999.99m || decimal.Round(threshold, 2) != threshold)
            return Invalid("invalidThreshold");
        var finance = string.IsNullOrEmpty(input.FinanceApprover) ? null : input.FinanceApprover;
        if (finance is not null && !await db.Memberships.AnyAsync(x =>
            x.OrganizationId == organizationId && x.Subject == finance && x.Role == "Approver", token))
            return Invalid("invalidFinance");
        organization.FinanceThreshold = threshold;
        organization.FinanceApprover = finance;
        Record(db, organizationId, actor.Subject, "routingUpdated", null);
        await db.SaveChangesAsync(token);
        await transaction.CommitAsync(token);
        return Results.NoContent();
    }

    private static void Record(FlowOpsDb db, Guid org, string actor, string action, string? target) =>
        db.OrganizationEvents.Add(new OrganizationEvent { OrganizationId = org, Actor = actor, Action = action, Target = target });
}

internal sealed record InviteInput(string? Email, string? Role);
internal sealed record AcceptInput(string? Code);
internal sealed record MemberInput(string? Role, string? Manager);
internal sealed record RoutingInput(decimal? Threshold, string? FinanceApprover);
