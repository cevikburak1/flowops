using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace FlowOps.Api;

internal static class WorkflowSimulationEndpoints
{
    private static readonly JsonSerializerOptions Json = JsonSerializerOptions.Web;
    private const int PageSize = 10;

    internal static void Map(RouteGroupBuilder tenant)
    {
        var routes = tenant.MapGroup("/workflows/{workflowId:guid}/simulation")
            .WithTags("Workflow simulation");
        routes.MapGet("", ListAsync);
        routes.MapPost("", RunAsync).WithMetadata(new RequestSizeLimitAttribute(4096))
            .Produces<SimulationResponse>(StatusCodes.Status201Created);
        routes.MapGet("/runs/{runId:guid}", GetAsync).Produces<SimulationResponse>();
    }

    private static Task<PurchaseWorkflowDefinition?> FindAsync(Guid org, Guid flow, ClaimsPrincipal user,
        FlowOpsDb db, CancellationToken token)
    {
        var subject = PurchaseEndpoints.Subject(user);
        return db.PurchaseWorkflows.AsNoTracking().SingleOrDefaultAsync(x => x.OrganizationId == org && x.Id == flow &&
            db.Memberships.Any(m => m.OrganizationId == org && m.Subject == subject &&
                (m.Role == "OrganizationOwner" || m.Role == "OrganizationAdmin")), token);
    }

    private static async Task<IResult> ListAsync(Guid organizationId, Guid workflowId, int? page, int? versionPage,
        ClaimsPrincipal user, FlowOpsDb db, CancellationToken token)
    {
        var workflow = await FindAsync(organizationId, workflowId, user, db, token);
        if (workflow is null) return Results.NotFound();
        var current = Math.Clamp(page ?? 1, 1, 10000);
        var currentVersions = Math.Clamp(versionPage ?? 1, 1, 10000);
        var versions = await db.PurchaseWorkflowVersions.AsNoTracking()
            .Where(x => x.OrganizationId == organizationId && x.WorkflowId == workflowId)
            .OrderByDescending(x => x.Number).Skip((currentVersions - 1) * PageSize).Take(PageSize + 1)
            .Select(x => x.Number).ToListAsync(token);
        var runs = await db.WorkflowSimulationRuns.AsNoTracking().Where(x => x.OrganizationId == organizationId && x.WorkflowId == workflowId)
            .OrderByDescending(x => x.CreatedAt).ThenByDescending(x => x.Id).Skip((current - 1) * PageSize).Take(PageSize + 1)
            .Select(x => new { x.Id, x.CreatedAt, x.Snapshot }).ToListAsync(token);
        var scenarios = await db.WorkflowSimulationScenarios.AsNoTracking()
            .Where(x => x.OrganizationId == organizationId && x.WorkflowId == workflowId)
            .OrderByDescending(x => x.CreatedAt).ThenByDescending(x => x.Id).Skip((current - 1) * PageSize).Take(PageSize + 1)
            .Select(x => new { x.Id, x.Name, x.Input }).ToListAsync(token);
        return Results.Ok(new { workflow.Name, workflow.Revision, Versions = versions.Take(PageSize),
            VersionPage = currentVersions, MoreVersions = versions.Count > PageSize, Page = current,
            HasMore = runs.Count > PageSize || scenarios.Count > PageSize,
            Runs = runs.Take(PageSize).Select(x => new { x.Id, x.CreatedAt,
                Summary = JsonSerializer.Deserialize<SimulationSnapshot>(x.Snapshot, Json)!.Result.Outcome }),
            Scenarios = scenarios.Take(PageSize).Select(x => new { x.Id, x.Name, Input = JsonSerializer.Deserialize<SimulationInput>(x.Input, Json) }) });
    }

    private static async Task<IResult> RunAsync(Guid organizationId, Guid workflowId, SimulationRequest request,
        ClaimsPrincipal user, FlowOpsDb db, CancellationToken token)
    {
        var workflow = await FindAsync(organizationId, workflowId, user, db, token);
        if (workflow is null) return Results.NotFound();
        var name = request.ScenarioName?.Trim();
        if (name is { Length: < 1 or > 80 } || request.Input is not { } input || !WorkflowSimulation.Valid(input))
            return Results.BadRequest(new { Code = "invalidInput" });
        PurchaseGraph graph;
        decimal threshold;
        if (input.Version is int number)
        {
            var version = await db.PurchaseWorkflowVersions.AsNoTracking().SingleOrDefaultAsync(x =>
                x.OrganizationId == organizationId && x.WorkflowId == workflowId && x.Number == number, token);
            if (version is null) return Results.NotFound();
            graph = PurchaseGraph.Read(version.ElsaDefinitionId is null ? null : version.GraphJson);
            threshold = version.FinanceThreshold;
        }
        else
        {
            if (input.Revision != workflow.Revision) return Results.Conflict(new { Code = "draftChanged" });
            graph = PurchaseGraph.Read(workflow.GraphJson);
            threshold = workflow.FinanceThreshold;
        }
        var snapshot = new SimulationSnapshot(input, threshold, graph, WorkflowSimulation.Run(graph, threshold, input, token));
        WorkflowSimulationScenario? scenario = null;
        if (name is not null)
        {
            scenario = new() { OrganizationId = organizationId, WorkflowId = workflowId, Name = name,
                Input = JsonSerializer.Serialize(input, Json) };
            db.WorkflowSimulationScenarios.Add(scenario);
        }
        var run = new WorkflowSimulationRun { OrganizationId = organizationId, WorkflowId = workflowId,
            Actor = PurchaseEndpoints.Subject(user), ScenarioId = scenario?.Id, Snapshot = JsonSerializer.Serialize(snapshot, Json) };
        db.WorkflowSimulationRuns.Add(run);
        await db.SaveChangesAsync(token);
        return Results.Created($"/api/organizations/{organizationId}/workflows/{workflowId}/simulation/runs/{run.Id}",
            new SimulationResponse(run.Id, run.CreatedAt, snapshot));
    }

    private static async Task<IResult> GetAsync(Guid organizationId, Guid workflowId, Guid runId, ClaimsPrincipal user,
        FlowOpsDb db, CancellationToken token)
    {
        if (await FindAsync(organizationId, workflowId, user, db, token) is null) return Results.NotFound();
        var run = await db.WorkflowSimulationRuns.AsNoTracking().SingleOrDefaultAsync(x =>
            x.OrganizationId == organizationId && x.WorkflowId == workflowId && x.Id == runId, token);
        return run is null ? Results.NotFound() : Results.Ok(new SimulationResponse(run.Id, run.CreatedAt,
            JsonSerializer.Deserialize<SimulationSnapshot>(run.Snapshot, Json)!));
    }

    internal sealed record SimulationRequest(SimulationInput? Input, string? ScenarioName);
    internal sealed record SimulationSnapshot(SimulationInput Input, decimal Threshold, PurchaseGraph Graph, SimulationResult Result);
    internal sealed record SimulationResponse(Guid Id, DateTimeOffset CreatedAt, SimulationSnapshot Snapshot);
}
