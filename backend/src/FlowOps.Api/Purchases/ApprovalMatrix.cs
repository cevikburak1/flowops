using System.Security.Claims;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;

namespace FlowOps.Api;

internal sealed record ApprovalMatrixRule(int Priority, string Currency, decimal Minimum, decimal? Maximum,
    string? Category, string? RequesterRole, Guid? DepartmentId, string Route,
    string[]? AdditionalReviewers = null, int? MinimumApprovals = null);

internal static class ApprovalMatrix
{
    internal static void Map(RouteGroupBuilder tenant)
    {
        tenant.MapGet("/approval-matrix", GetAsync);
        tenant.MapPost("/approval-matrix", SaveAsync);
        tenant.MapPost("/approval-matrix/preview", PreviewAsync);
    }

    internal static ApprovalMatrixRule[] Read(string? json) =>
        json is null ? [] : JsonSerializer.Deserialize<ApprovalMatrixRule[]>(json, JsonSerializerOptions.Web)!;

    private static async Task<IResult> GetAsync(Guid organizationId, ClaimsPrincipal user, FlowOpsDb db, CancellationToken token)
    {
        if (await OrganizationEndpoints.FindAdminAsync(db, organizationId, user, token) is null) return Results.NotFound();
        var org = await db.Organizations.SingleAsync(row => row.Id == organizationId, token);
        return Results.Ok(new { org.Currency, org.ApprovalMatrixRevision, Rules = Read(org.ApprovalMatrixJson) });
    }

    private static async Task<IResult> SaveAsync(Guid organizationId, MatrixChange input, ClaimsPrincipal user,
        FlowOpsDb db, CancellationToken token)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(token);
        var org = await OrganizationEndpoints.LockAsync(db, organizationId, token);
        var actor = await OrganizationEndpoints.FindAdminAsync(db, organizationId, user, token);
        if (org is null || actor is null) return Results.NotFound();
        if (input.Revision != org.ApprovalMatrixRevision) return Results.Conflict(new { Code = "matrixChanged" });
        if (input.Rules is not { } rules || !Valid(rules, org.Currency)) return Results.BadRequest(new { Code = "invalidMatrix" });
        var departments = rules.Select(rule => rule!.DepartmentId).OfType<Guid>().Distinct().ToArray();
        if (await db.OrganizationGroups.CountAsync(row => row.OrganizationId == organizationId && row.Kind == "department" &&
            departments.Contains(row.Id), token) != departments.Length) return Results.BadRequest(new { Code = "invalidMatrix" });
        var reviewers = rules.SelectMany(rule => rule!.AdditionalReviewers ?? []).Distinct().ToArray();
        if (await db.Memberships.CountAsync(row => row.OrganizationId == organizationId && reviewers.Contains(row.Subject) &&
            PurchaseEndpoints.ManagerRoles.Contains(row.Role), token) != reviewers.Length)
            return Results.BadRequest(new { Code = "invalidMatrix" });
        org.ApprovalMatrixJson = JsonSerializer.Serialize(rules.OrderBy(rule => rule!.Priority), JsonSerializerOptions.Web);
        org.ApprovalMatrixRevision++;
        db.OrganizationEvents.Add(new() { OrganizationId = organizationId, Actor = actor.Subject, Action = "approvalMatrixUpdated" });
        await db.SaveChangesAsync(token);
        await transaction.CommitAsync(token);
        return Results.Ok(new { org.ApprovalMatrixRevision });
    }

    private static async Task<IResult> PreviewAsync(Guid organizationId, MatrixPreview input, ClaimsPrincipal user,
        FlowOpsDb db, CancellationToken token)
    {
        if (await OrganizationEndpoints.FindAdminAsync(db, organizationId, user, token) is null) return Results.NotFound();
        var org = await db.Organizations.SingleAsync(row => row.Id == organizationId, token);
        if (input.Amount is <= 0 or > 999_999_999.99m || input.Rules is not { } rules || !Valid(rules, org.Currency))
            return Results.BadRequest(new { Code = "invalidMatrix" });
        var rule = Match(rules.OfType<ApprovalMatrixRule>(), org.Currency, input.Amount, input.Category, input.Role, input.DepartmentId);
        return Results.Ok(new { Priority = rule?.Priority, Route = rule?.Route ??
            (input.Amount > org.FinanceThreshold ? "managerFinance" : "manager") });
    }

    internal sealed record MatrixChange(int Revision, ApprovalMatrixRule?[]? Rules);
    internal sealed record MatrixPreview(decimal Amount, string Category, string Role, Guid? DepartmentId, ApprovalMatrixRule?[]? Rules);
    public static bool Valid(ApprovalMatrixRule?[] rules, string currency) => rules.Length <= 30 &&
        rules.All(rule => rule is not null && rule.Priority is >= 1 and <= 1000 && rule.Currency == currency &&
            rule.Minimum is >= 0 and <= 999_999_999.99m &&
            (rule.Maximum is null || rule.Maximum > rule.Minimum && rule.Maximum <= 1_000_000_000m) &&
            rule.Category is null or "equipment" or "software" or "services" or "supplies" or "other" &&
            rule.RequesterRole is null or "OrganizationOwner" or "OrganizationAdmin" or "Manager" or "Employee" or "Approver" &&
            rule.Route is "manager" or "managerFinance" && ValidQuorum(rule)) &&
        rules.Select(rule => rule!.Priority).Distinct().Count() == rules.Length;

    private static bool ValidQuorum(ApprovalMatrixRule rule) =>
        rule.AdditionalReviewers is null or { Length: 0 } ? rule.MinimumApprovals is null or 1 :
            rule.AdditionalReviewers is { Length: <= 8 } reviewers &&
            reviewers.All(subject => subject is { Length: >= 1 and <= 255 }) &&
            reviewers.Distinct(StringComparer.Ordinal).Count() == reviewers.Length &&
            rule.MinimumApprovals is >= 1 && rule.MinimumApprovals <= reviewers.Length + 1;

    // Lower priorities win. Bounds are inclusive at the minimum and exclusive at the maximum.
    public static ApprovalMatrixRule? Match(IEnumerable<ApprovalMatrixRule> rules, string currency, decimal amount,
        string category, string role, Guid? department) => rules
        .Where(rule => rule.Currency == currency && amount >= rule.Minimum && (rule.Maximum is null || amount < rule.Maximum) &&
            (rule.Category is null || rule.Category == category) &&
            (rule.RequesterRole is null || rule.RequesterRole == role) &&
            (rule.DepartmentId is null || rule.DepartmentId == department))
        .OrderBy(rule => rule.Priority).FirstOrDefault();
}
