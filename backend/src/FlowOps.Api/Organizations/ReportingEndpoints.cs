using System.Security.Claims;
using Microsoft.EntityFrameworkCore;

namespace FlowOps.Api;

internal static class ReportingEndpoints
{
    public static void Map(RouteGroupBuilder api, RouteGroupBuilder tenant)
    {
        tenant.MapGet("/reports", ReportAsync);
        api.MapGet("/platform/organizations", PlatformAsync);
        api.MapPost("/platform/organizations/{organizationId:guid}/plan", SetPlanAsync);
    }

    private static async Task<IResult> ReportAsync(Guid organizationId, int? days, ClaimsPrincipal user, FlowOpsDb db,
        CancellationToken token)
    {
        if (await OrganizationEndpoints.FindAdminAsync(db, organizationId, user, token) is null) return Results.NotFound();
        var window = days is 7 or 90 ? days.Value : 30;
        var since = DateTimeOffset.UtcNow.AddDays(-window);
        var purchases = db.Purchases.AsNoTracking().Where(x => x.OrganizationId == organizationId && x.CreatedAt >= since);
        var statuses = await purchases.GroupBy(x => x.Status).Select(g => new
        {
            Status = g.Key, Count = g.Count(), Amount = g.Sum(x => x.Amount)
        }).OrderBy(x => x.Status).ToListAsync(token);
        var overdue = await db.Approvals.CountAsync(x => x.OrganizationId == organizationId &&
            x.Decision == null && x.DueAt <= DateTimeOffset.UtcNow, token);
        var members = await db.Memberships.CountAsync(x => x.OrganizationId == organizationId, token);
        var workflows = await db.PurchaseWorkflows.CountAsync(x => x.OrganizationId == organizationId, token);
        var plan = await db.Organizations.Where(x => x.Id == organizationId).Select(x => x.Plan).SingleAsync(token);
        var organizationEvents = db.OrganizationEvents.Where(x => x.OrganizationId == organizationId && x.At >= since)
            .Select(x => new { x.Id, x.Actor, x.Action, x.Target, x.At });
        var purchaseEvents = db.Audit.Where(x => x.OrganizationId == organizationId && x.At >= since)
            .Select(x => new { x.Id, x.Actor, x.Action, Target = (string?)x.PurchaseId.ToString(), x.At });
        var audit = await organizationEvents.Concat(purchaseEvents).OrderByDescending(x => x.At).ThenBy(x => x.Id)
            .Take(50).ToListAsync(token);
        var deliveries = await (from delivery in db.NotificationDeliveries
            join notification in db.Notifications on delivery.NotificationId equals notification.Id
            where notification.OrganizationId == organizationId && delivery.CompletedAt == null && delivery.Attempts > 0
            group delivery by delivery.Channel into channel
            orderby channel.Key
            select new { Channel = channel.Key, Count = channel.Count(), NextAttemptAt = channel.Min(x => x.NextAttemptAt) })
            .ToListAsync(token);
        return Results.Ok(new { Days = window, Statuses = statuses, Overdue = overdue, Members = members,
            Workflows = workflows, Plan = plan, Audit = audit, Deliveries = deliveries });
    }

    internal static bool IsOperator(ClaimsPrincipal user, IConfiguration config) =>
        config.GetSection("Platform:OperatorSubjects").Get<string[]>()?.Contains(PurchaseEndpoints.Subject(user), StringComparer.Ordinal) == true;

    private static async Task<IResult> PlatformAsync(int? page, ClaimsPrincipal user, IConfiguration config, FlowOpsDb db,
        CancellationToken token)
    {
        if (!IsOperator(user, config)) return Results.NotFound();
        const int limit = 25;
        var current = Math.Clamp(page ?? 1, 1, 10000);
        var organizations = await db.Organizations.AsNoTracking().OrderBy(x => x.Name).ThenBy(x => x.Id)
            .Skip((current - 1) * limit).Take(limit + 1)
            .Select(x => new { x.Id, x.Name, x.Slug, x.Plan,
                Members = db.Memberships.Count(m => m.OrganizationId == x.Id),
                Workflows = db.PurchaseWorkflows.Count(w => w.OrganizationId == x.Id) }).ToListAsync(token);
        return Results.Ok(new { Items = organizations.Take(limit), Page = current, HasMore = organizations.Count > limit });
    }

    private static async Task<IResult> SetPlanAsync(Guid organizationId, PlanInput input, ClaimsPrincipal user,
        IConfiguration config, FlowOpsDb db, CancellationToken token)
    {
        if (!IsOperator(user, config)) return Results.NotFound();
        if (input.Plan is not ("starter" or "business" or "enterprise"))
            return Results.BadRequest(new { code = "invalidPlan" });
        await using var transaction = await db.Database.BeginTransactionAsync(token);
        var organization = await OrganizationEndpoints.LockAsync(db, organizationId, token);
        if (organization is null) return Results.NotFound();
        if (organization.Plan == input.Plan) return Results.NoContent();
        organization.Plan = input.Plan;
        db.OrganizationEvents.Add(new OrganizationEvent { OrganizationId = organizationId,
            Actor = PurchaseEndpoints.Subject(user), Action = "planChanged", Target = input.Plan });
        await db.SaveChangesAsync(token);
        await transaction.CommitAsync(token);
        return Results.NoContent();
    }

    private sealed record PlanInput(string? Plan);
}
