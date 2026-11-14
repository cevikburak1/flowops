using System.Security.Claims;
using Microsoft.EntityFrameworkCore;

namespace FlowOps.Api;

internal sealed class CopilotQuestion
{
    public Guid OrganizationId { get; init; }
    public Guid WorkflowId { get; init; }
    public required string Subject { get; init; }
    public Guid Id { get; init; } = Guid.NewGuid();
    public required string Text { get; init; }
    public string? Answer { get; init; }
    public DateTimeOffset CreatedAt { get; init; }
    public DateTimeOffset ExpiresAt { get; init; }
}

internal static class CopilotHistory
{
    private const int Capacity = 20;

    internal static void Map(RouteGroupBuilder routes)
    {
        var history = routes.MapGroup("/workflows/{workflowId:guid}/history");
        history.MapGet("", ListAsync);
        history.MapPost("", SaveAsync);
        history.MapDelete("", ClearAsync);
    }

    private static async Task<bool> Allowed(Guid org, Guid workflow, ClaimsPrincipal user, FlowOpsDb db, CancellationToken token) =>
        await OrganizationEndpoints.FindAdminAsync(db, org, user, token) is not null &&
        await db.PurchaseWorkflows.AnyAsync(row => row.OrganizationId == org && row.Id == workflow && !row.Archived, token);

    private static IQueryable<CopilotQuestion> Own(FlowOpsDb db, Guid org, Guid workflow, ClaimsPrincipal user) =>
        db.CopilotQuestions.Where(row => row.OrganizationId == org && row.WorkflowId == workflow &&
            row.Subject == PurchaseEndpoints.Subject(user));

    internal static async Task<IResult> ListAsync(Guid organizationId, Guid workflowId, ClaimsPrincipal user,
        FlowOpsDb db, IConfiguration config, CancellationToken token)
    {
        if (!await Allowed(organizationId, workflowId, user, db, token)) return Results.NotFound();
        var days = Days(config);
        var now = DateTimeOffset.UtcNow;
        var items = await Own(db, organizationId, workflowId, user)
            .Where(row => row.ExpiresAt > now)
            .OrderByDescending(row => row.CreatedAt).ThenByDescending(row => row.Id)
            .Take(Capacity).Select(row => new { row.Id, row.Text, row.Answer, row.CreatedAt }).ToListAsync(token);
        return Results.Ok(new { RetentionDays = days, Items = items });
    }

    internal static async Task<IResult> SaveAsync(Guid organizationId, Guid workflowId, SavedQuestionInput input,
        ClaimsPrincipal user, FlowOpsDb db, IConfiguration config, CancellationToken token)
    {
        if (!await Allowed(organizationId, workflowId, user, db, token)) return Results.NotFound();
        var days = Days(config);
        if (days == 0) return Results.Conflict(new { Code = "historyDisabled" });
        if (input.Prompt?.Trim() is not { Length: >= 10 and <= 2000 } text || input.Answer?.Length > 4000)
            return Results.BadRequest(new { Code = "invalidPrompt" });
        await using var transaction = await db.Database.BeginTransactionAsync(token);
        if (await OrganizationEndpoints.LockAsync(db, organizationId, token) is null ||
            !await Allowed(organizationId, workflowId, user, db, token)) return Results.NotFound();
        var rows = Own(db, organizationId, workflowId, user);
        var now = DateTimeOffset.UtcNow;
        await rows.Where(row => row.ExpiresAt <= now || row.Text == text).ExecuteDeleteAsync(token);
        var excess = rows.OrderByDescending(row => row.CreatedAt).ThenByDescending(row => row.Id).Skip(Capacity - 1);
        await excess.ExecuteDeleteAsync(token);
        db.CopilotQuestions.Add(new() { OrganizationId = organizationId, WorkflowId = workflowId,
            Subject = PurchaseEndpoints.Subject(user), Text = text, Answer = input.Answer,
            CreatedAt = now, ExpiresAt = now.AddDays(days) });
        await db.SaveChangesAsync(token);
        await transaction.CommitAsync(token);
        return Results.NoContent();
    }

    internal static async Task<IResult> ClearAsync(Guid organizationId, Guid workflowId, ClaimsPrincipal user,
        FlowOpsDb db, CancellationToken token)
    {
        if (!await Allowed(organizationId, workflowId, user, db, token)) return Results.NotFound();
        await using var transaction = await db.Database.BeginTransactionAsync(token);
        if (await OrganizationEndpoints.LockAsync(db, organizationId, token) is null ||
            !await Allowed(organizationId, workflowId, user, db, token)) return Results.NotFound();
        await Own(db, organizationId, workflowId, user).ExecuteDeleteAsync(token);
        await transaction.CommitAsync(token);
        return Results.NoContent();
    }

    internal static int Days(IConfiguration config) => Math.Clamp(config.GetValue("Copilot:HistoryDays", 0), 0, 90);
    internal sealed record SavedQuestionInput(string? Prompt, string? Answer = null);
}

internal sealed class CopilotHistoryCleanup(IServiceScopeFactory scopes, ILogger<CopilotHistoryCleanup> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromHours(1));
        do
        {
            try
            {
                await using var scope = scopes.CreateAsyncScope();
                var db = scope.ServiceProvider.GetRequiredService<FlowOpsDb>();
                var now = DateTimeOffset.UtcNow;
                await db.CopilotQuestions.Where(row => row.ExpiresAt <= now).ExecuteDeleteAsync(stoppingToken);
            }
            catch (Exception ex) when (!stoppingToken.IsCancellationRequested)
            {
                logger.LogWarning("Copilot history cleanup failed ({ErrorType}).", ex.GetType().Name);
            }
        } while (await timer.WaitForNextTickAsync(stoppingToken));
    }
}
