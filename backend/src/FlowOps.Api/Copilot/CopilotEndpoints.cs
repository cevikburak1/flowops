using System.Diagnostics;
using System.Security.Claims;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.EntityFrameworkCore;

namespace FlowOps.Api;

internal sealed class CopilotUsage
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public Guid OrganizationId { get; init; }
    public required string Subject { get; init; }
    public required string Model { get; init; }
    public required string Feature { get; init; }
    public DateTimeOffset At { get; init; } = DateTimeOffset.UtcNow;
    public string Status { get; set; } = "reserved";
    public int? InputTokens { get; set; }
    public int? OutputTokens { get; set; }
    public decimal? EstimatedCostUsd { get; set; }
    public long DurationMs { get; set; }
}

internal sealed record CopilotProposal(string? Name, string? Route, decimal? FinanceThreshold, string[]? Limitations)
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
        MaxDepth = 8
    };

    internal static CopilotProposal? Parse(string output) => JsonSerializer.Deserialize<CopilotProposal>(output, Json);

    internal PurchaseGraph? Graph() => Route switch
    {
        "manager" => new(1, [new("start", "start", 0, 110), new("manager", "manager", 210, 110),
            new("end", "end", 420, 110)], [new("start", "manager"), new("manager", "end")]),
        "managerNotify" => new(1, [new("start", "start", 0, 110), new("manager", "manager", 210, 110),
            new("notify", "notify", 350, 110), new("end", "end", 500, 110)],
            [new("start", "manager"), new("manager", "notify"), new("notify", "end")]),
        "managerFinance" => PurchaseGraph.Default,
        "managerFinanceNotify" => PurchaseGraph.Default with
        {
            Nodes = [new("start", "start", 0, 110), new("manager", "manager", 210, 110), new("threshold", "threshold", 420, 110),
                new("finance", "finance", 630, 0), new("notify", "notify", 780, 0), new("end", "end", 930, 110)],
            Edges = [new("start", "manager"), new("manager", "threshold"), new("threshold", "end", "below"),
                new("threshold", "finance", "above"), new("finance", "notify"), new("notify", "end")]
        },
        _ => null
    };

    internal bool Valid() => Name?.Trim().Length is >= 3 and <= 120 &&
        Limitations is { Length: <= 8 } && Limitations.All(text => text?.Length is > 0 and <= 400) &&
        (Route == "unsupported" ? Limitations.Length > 0 : Limitations.Length == 0 &&
            FinanceThreshold is >= 0 and <= 999_999_999.99m &&
            decimal.Round(FinanceThreshold.Value, 2) == FinanceThreshold &&
            (Route is not ("manager" or "managerNotify") || FinanceThreshold == 0) && Graph()?.CanPublish() == true);
}

internal static class CopilotEndpoints
{
    private const string Feature = "purchaseWorkflowGeneration";

    internal static void Map(RouteGroupBuilder tenant)
    {
        var routes = tenant.MapGroup("/copilot/v1");
        routes.MapGet("/status", StatusAsync);
        routes.MapGet("/usage", UsageAsync);
        routes.MapGet("/settings", SettingsAsync);
        routes.MapPatch("/settings", UpdateSettingsAsync);
        routes.MapPost("/purchase-proposals", GenerateAsync);
        CopilotTools.Map(routes);
        CopilotHistory.Map(routes);
    }

    private static bool Enabled(IConfiguration config, Guid organizationId, Organization? organization = null) =>
        (organization?.CopilotEnabled ?? config.GetValue<bool>("Copilot:Enabled")) &&
        !string.IsNullOrWhiteSpace(config["Copilot:ApiKey"]) &&
        (string.IsNullOrWhiteSpace(config["Copilot:Organizations"]) ||
            config["Copilot:Organizations"]!.Split(',', StringSplitOptions.TrimEntries)
                .Any(value => Guid.TryParse(value, out var id) && id == organizationId)) &&
        config["Copilot:Model"] is { Length: > 0 and <= 120 } model &&
        model.All(character => char.IsAsciiLetterOrDigit(character) || character is '-' or '_' or '.' or ':');

    private static async Task<IResult> StatusAsync(Guid organizationId, ClaimsPrincipal user, FlowOpsDb db,
        IConfiguration config, CancellationToken token)
    {
        if (await OrganizationEndpoints.FindAdminAsync(db, organizationId, user, token) is null) return Results.NotFound();
        var organization = await db.Organizations.AsNoTracking().SingleAsync(row => row.Id == organizationId, token);
        var now = DateTimeOffset.UtcNow;
        var month = new DateTimeOffset(now.Year, now.Month, 1, 0, 0, 0, TimeSpan.Zero);
        var usage = db.CopilotUsages.Where(row => row.OrganizationId == organizationId && row.At >= month);
        var totals = await usage.GroupBy(_ => 1).Select(rows => new
        {
            Requests = rows.Count(), InputTokens = rows.Sum(row => (long?)row.InputTokens),
            OutputTokens = rows.Sum(row => (long?)row.OutputTokens),
            Cost = rows.Sum(row => row.EstimatedCostUsd), CostSamples = rows.Count(row => row.EstimatedCostUsd != null)
        }).SingleOrDefaultAsync(token);
        return Results.Ok(new { Enabled = Enabled(config, organizationId, organization), MonthlyLimit = MonthlyLimit(config, organization),
            HistoryDays = CopilotHistory.Days(config, organization),
            Requests = totals?.Requests ?? 0, totals?.InputTokens, totals?.OutputTokens,
            EstimatedCostUsd = totals?.CostSamples > 0 ? totals.Cost : null });
    }

    private static async Task<IResult> SettingsAsync(Guid organizationId, ClaimsPrincipal user, FlowOpsDb db,
        IConfiguration config, CancellationToken token)
    {
        if (await OrganizationEndpoints.FindAdminAsync(db, organizationId, user, token) is null) return Results.NotFound();
        var organization = await db.Organizations.AsNoTracking().SingleAsync(row => row.Id == organizationId, token);
        return Results.Ok(new { Enabled = organization.CopilotEnabled ?? config.GetValue<bool>("Copilot:Enabled"),
            MonthlyRequests = organization.CopilotMonthlyRequests ?? MonthlyLimit(config, null),
            HistoryDays = organization.CopilotHistoryDays ?? config.GetValue("Copilot:HistoryDays", 0),
            OperatorEnabled = config.GetValue<bool>("Copilot:Enabled"),
            OperatorModelConfigured = !string.IsNullOrWhiteSpace(config["Copilot:Model"]) });
    }

    private static async Task<IResult> UpdateSettingsAsync(Guid organizationId, CopilotSettingsInput input,
        ClaimsPrincipal user, FlowOpsDb db, IConfiguration config, CancellationToken token)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(token);
        var organization = await OrganizationEndpoints.LockAsync(db, organizationId, token);
        var actor = await OrganizationEndpoints.FindAdminAsync(db, organizationId, user, token);
        if (organization is null || actor is null) return Results.NotFound();
        if (input.Enabled is null) organization.CopilotEnabled = null;
        else organization.CopilotEnabled = input.Enabled.Value;
        if (input.MonthlyRequests is not (null or >= 1 and <= 10000)) return Results.BadRequest(new { Code = "invalidMonthlyRequests" });
        if (input.HistoryDays is not (null or >= 0 and <= 90)) return Results.BadRequest(new { Code = "invalidHistoryDays" });
        organization.CopilotMonthlyRequests = input.MonthlyRequests;
        organization.CopilotHistoryDays = input.HistoryDays;
        OrganizationEndpoints.Record(db, organizationId, actor.Subject, "copilotSettingsUpdated", null);
        await db.SaveChangesAsync(token);
        await transaction.CommitAsync(token);
        return Results.Ok(new { Enabled = organization.CopilotEnabled ?? config.GetValue<bool>("Copilot:Enabled"),
            MonthlyRequests = organization.CopilotMonthlyRequests ?? MonthlyLimit(config, null),
            HistoryDays = organization.CopilotHistoryDays ?? config.GetValue("Copilot:HistoryDays", 0) });
    }

    private static async Task<IResult> UsageAsync(Guid organizationId, int? page, ClaimsPrincipal user, FlowOpsDb db,
        CancellationToken token)
    {
        if (await OrganizationEndpoints.FindAdminAsync(db, organizationId, user, token) is null) return Results.NotFound();
        const int size = 20;
        var current = Math.Clamp(page ?? 1, 1, 500);
        var entries = await db.CopilotUsages.AsNoTracking().Where(row => row.OrganizationId == organizationId)
            .OrderByDescending(row => row.At).ThenBy(row => row.Id).Skip((current - 1) * size).Take(size + 1)
            .Select(row => new { row.Feature, row.Model, row.At, row.Status, row.InputTokens, row.OutputTokens,
                row.EstimatedCostUsd, row.DurationMs }).ToListAsync(token);
        return Results.Ok(new { Page = current, HasMore = entries.Count > size, Items = entries.Take(size) });
    }

    internal static Task<IResult> GenerateAsync(Guid organizationId, CopilotInput input, ClaimsPrincipal user,
        FlowOpsDb db, IConfiguration config, ICopilotProvider provider, CancellationToken token) =>
        RunAsync(organizationId, input, user, db, config, provider, token, Feature, null, null, ReviewWorkflow);

    internal static async Task<IResult> RunAsync(Guid organizationId, CopilotInput input, ClaimsPrincipal user,
        FlowOpsDb db, IConfiguration config, ICopilotProvider provider, CancellationToken token, string feature,
        CopilotContract? contract, object? context, Func<string, CopilotUsage, CancellationToken, IResult> review)
    {
        if (await OrganizationEndpoints.FindAdminAsync(db, organizationId, user, token) is not { } member) return Results.NotFound();
        var organizationSettings = await db.Organizations.AsNoTracking().SingleOrDefaultAsync(row => row.Id == organizationId, token);
        if (!Enabled(config, organizationId, organizationSettings)) return Error(503, "copilotUnavailable");
        if (input.Prompt?.Trim() is not { Length: >= 10 and <= 2000 } prompt) return Error(400, "invalidPrompt");

        var usage = new CopilotUsage { OrganizationId = organizationId, Subject = member.Subject,
            Model = config["Copilot:Model"]!, Feature = feature };
        {
            await using var transaction = await db.Database.BeginTransactionAsync(token);
            if (await OrganizationEndpoints.LockAsync(db, organizationId, token) is not { } org ||
                await OrganizationEndpoints.FindAdminAsync(db, organizationId, user, token) is null) return Results.NotFound();
            var now = usage.At;
            var month = new DateTimeOffset(now.Year, now.Month, 1, 0, 0, 0, TimeSpan.Zero);
            var rows = db.CopilotUsages.Where(row => row.OrganizationId == organizationId);
            if (await rows.CountAsync(row => row.At >= month, token) >= MonthlyLimit(config, org) ||
                await rows.CountAsync(row => row.Subject == member.Subject && row.At >= now.AddMinutes(-1), token) >= 4)
                return Error(429, "copilotLimit");
            db.CopilotUsages.Add(usage);
            await db.SaveChangesAsync(token);
            await transaction.CommitAsync(token);
            prompt = $"Organization currency: {org.Currency}. Requirements follow:\n{prompt}";
        }

        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(token);
        timeout.CancelAfter(TimeSpan.FromSeconds(Math.Clamp(config.GetValue("Copilot:TimeoutSeconds", 20), 2, 60)));
        var watch = Stopwatch.StartNew();
        try
        {
            var payload = JsonSerializer.Serialize(new { requirement = prompt, context });
            var response = await provider.GenerateAsync(payload, timeout.Token, contract);
            usage.Status = response.Status;
            usage.InputTokens = response.InputTokens;
            usage.OutputTokens = response.OutputTokens;
            usage.EstimatedCostUsd = Cost(response, config);
            if (response.Output is null) return Error(502, response.Status);
            return review(response.Output, usage, timeout.Token);
        }
        catch (OperationCanceledException)
        {
            usage.Status = token.IsCancellationRequested ? "canceled" : "timeout";
            if (token.IsCancellationRequested) throw;
            return Error(504, "copilotTimeout");
        }
        catch (JsonException)
        {
            usage.Status = "invalidProposal";
            return Error(422, usage.Status);
        }
        catch (Exception ex) when (ex is HttpRequestException or InvalidOperationException or KeyNotFoundException)
        {
            usage.Status = "providerError";
            return Error(502, usage.Status);
        }
        finally
        {
            usage.DurationMs = watch.ElapsedMilliseconds;
            using var recordTimeout = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            await db.SaveChangesAsync(recordTimeout.Token);
        }
    }

    private static IResult ReviewWorkflow(string output, CopilotUsage usage, CancellationToken token)
    {
        var proposal = CopilotProposal.Parse(output);
        if (proposal?.Valid() != true)
        {
            usage.Status = "invalidProposal";
            return Error(422, usage.Status);
        }
        if (proposal.Graph() is not { } graph)
        {
            usage.Status = "unsupportedRequirements";
            return Results.UnprocessableEntity(new { Code = usage.Status, proposal.Limitations });
        }
        var threshold = proposal.FinanceThreshold!.Value;
        var amount = threshold < 999_999_999.99m ? threshold + 0.01m : threshold;
        var simulation = WorkflowSimulation.Run(graph, threshold, new(null, 1, amount, "approved", "approved"), token);
        return Results.Ok(new { SchemaVersion = 1, Draft = new WorkflowDraft(proposal.Name, threshold, 0, graph), Simulation = simulation });
    }

    internal static decimal? Cost(CopilotResult usage, IConfiguration config)
    {
        var input = config.GetValue<decimal?>("Copilot:InputUsdPerMillion");
        var output = config.GetValue<decimal?>("Copilot:OutputUsdPerMillion");
        return input is >= 0 && output is >= 0 && usage.InputTokens is >= 0 && usage.OutputTokens is >= 0
            ? (input * usage.InputTokens + output * usage.OutputTokens) / 1_000_000m : null;
    }

    internal static int MonthlyLimit(IConfiguration config, Organization? organization) => Math.Clamp(organization?.CopilotMonthlyRequests ?? config.GetValue("Copilot:MonthlyRequests", 30), 1, 10000);
    private static IResult Error(int status, string code) => Results.Json(new { code }, statusCode: status);
    internal sealed record CopilotInput(string? Prompt);
    internal sealed record CopilotSettingsInput(bool? Enabled, int? MonthlyRequests, int? HistoryDays);
}


