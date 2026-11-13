using System.Security.Claims;
using Elsa.Workflows.Management;
using Microsoft.EntityFrameworkCore;

namespace FlowOps.Api;

internal static class WorkflowCatalog
{
    public static void Map(RouteGroupBuilder tenant)
    {
        var catalog = tenant.MapGroup("/workflows");
        catalog.MapGet("/", ListAsync);
        catalog.MapGet("/{workflowId:guid}", DetailsAsync);
        catalog.MapGet("/{workflowId:guid}/published", GetPublishedAsync);
        catalog.MapPost("/", CreateAsync);
        catalog.MapPost("/{workflowId:guid}/draft", SaveDraftAsync);
        catalog.MapPost("/{workflowId:guid}/publish", PublishAsync);
        catalog.MapPost("/{workflowId:guid}/unpublish", UnpublishAsync);
        catalog.MapPost("/{workflowId:guid}/duplicate", DuplicateAsync);
        catalog.MapPost("/{workflowId:guid}/archive", ArchiveAsync);
    }

    private static async Task<IResult> GetPublishedAsync(Guid organizationId, Guid workflowId, ClaimsPrincipal user, FlowOpsDb db, CancellationToken token)
    {
        if (await MemberAsync(db, organizationId, user, token) is null) return Results.NotFound();
        var version = await PublishedAsync(db, organizationId, workflowId, token);
        return version is null ? Results.NotFound() : Results.Ok(new { Id = workflowId, version.Name, version.Number });
    }

    private static Task<Membership?> MemberAsync(FlowOpsDb db, Guid organizationId, ClaimsPrincipal user, CancellationToken token)
    {
        var subject = PurchaseEndpoints.Subject(user);
        return db.Memberships.SingleOrDefaultAsync(x => x.OrganizationId == organizationId && x.Subject == subject, token);
    }

    private static bool CanManage(Membership? member) => member?.Role is "OrganizationOwner" or "OrganizationAdmin";

    private static async Task<IResult> ListAsync(Guid organizationId, int? page, string? q, string? status, ClaimsPrincipal user, FlowOpsDb db, CancellationToken token)
    {
        var member = await MemberAsync(db, organizationId, user, token);
        if (member is null) return Results.NotFound();
        var canManage = CanManage(member);
        var current = Math.Clamp(page ?? 1, 1, 10000);
        const int size = 10;
        var search = q?.Trim();
        if (search?.Length > 120 || status is not (null or "" or "draft" or "published" or "unpublished" or "archived"))
            return Results.BadRequest(new { code = "invalidFilter" });
        var flows = db.PurchaseWorkflows.AsNoTracking().Where(flow => flow.OrganizationId == organizationId &&
            (canManage || (!flow.Archived && !flow.Unpublished && flow.PublishedVersion != null)));
        flows = status switch
        {
            "draft" => flows.Where(flow => !flow.Archived && flow.PublishedVersion == null),
            "published" => flows.Where(flow => !flow.Archived && !flow.Unpublished && flow.PublishedVersion != null),
            "unpublished" => flows.Where(flow => !flow.Archived && flow.Unpublished),
            "archived" => flows.Where(flow => flow.Archived),
            _ => flows
        };
        var catalog = from flow in flows
            let publishedName = db.PurchaseWorkflowVersions.Where(version => version.OrganizationId == organizationId &&
                version.WorkflowId == flow.Id && version.Number == flow.PublishedVersion).Select(version => version.Name).FirstOrDefault()
            select new { flow.Id, Name = canManage ? flow.Name : publishedName, flow.Archived, flow.PublishedVersion, flow.Unpublished,
                HasDraft = canManage && flow.PublishedRevision != flow.Revision };
        if (!string.IsNullOrEmpty(search))
        {
            var pattern = "%" + search.Replace("!", "!!").Replace("%", "!%").Replace("_", "!_") + "%";
            catalog = catalog.Where(flow => EF.Functions.ILike(flow.Name!, pattern, "!"));
        }
        var items = await catalog.OrderBy(flow => flow.Name).ThenBy(flow => flow.Id)
            .Skip((current - 1) * size).Take(size + 1).ToListAsync(token);
        return Results.Ok(new { Items = items.Take(size), Page = current, HasMore = items.Count > size });
    }

    private static async Task<IResult> DetailsAsync(Guid organizationId, Guid workflowId, int? page, int? versionPage, ClaimsPrincipal user, FlowOpsDb db, CancellationToken token)
    {
        if (!CanManage(await MemberAsync(db, organizationId, user, token))) return Results.NotFound();
        var flow = await db.PurchaseWorkflows.AsNoTracking().SingleOrDefaultAsync(x => x.OrganizationId == organizationId && x.Id == workflowId, token);
        if (flow is null) return Results.NotFound();
        var current = Math.Clamp(page ?? 1, 1, 10000);
        var currentVersions = Math.Clamp(versionPage ?? page ?? 1, 1, 10000);
        const int size = 10;
        var versions = await db.PurchaseWorkflowVersions.AsNoTracking().Where(x => x.OrganizationId == organizationId && x.WorkflowId == workflowId)
            .OrderByDescending(x => x.Number).Skip((currentVersions - 1) * size).Take(size + 1)
            .Select(x => new { x.Number, x.Name, x.FinanceThreshold, x.PublishedAt }).ToListAsync(token);
        var runs = await db.Purchases.AsNoTracking().Where(x => x.OrganizationId == organizationId && x.WorkflowId == workflowId)
            .OrderByDescending(x => x.CreatedAt).ThenBy(x => x.Id).Skip((current - 1) * size).Take(size + 1)
            .Select(x => new { x.Id, x.Title, x.Status, x.WorkflowVersion, x.CreatedAt }).ToListAsync(token);
        return Results.Ok(new { flow.Id, flow.Name, flow.FinanceThreshold, flow.Revision, flow.Archived, flow.PublishedVersion, flow.Unpublished,
            Graph = PurchaseGraph.Read(flow.GraphJson),
            HasDraft = flow.PublishedRevision != flow.Revision, Versions = versions.Take(size), Runs = runs.Take(size),
            Page = current, HasMore = versions.Count > size || runs.Count > size,
            RunsHaveMore = runs.Count > size, VersionPage = currentVersions, VersionsHaveMore = versions.Count > size });
    }

    internal static async Task<IResult> CreateAsync(Guid organizationId, WorkflowDraft input, ClaimsPrincipal user, FlowOpsDb db,
        PlanEntitlements entitlements, CancellationToken token)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(token);
        if (await OrganizationEndpoints.LockAsync(db, organizationId, token) is not { } organization ||
            !CanManage(await MemberAsync(db, organizationId, user, token))) return Results.NotFound();
        if (!IsValid(input)) return Results.BadRequest(new { code = "invalidDraft" });
        if (!await entitlements.CanAddWorkflowAsync(db, organization, token))
            return Results.Conflict(new { code = "workflowPlanLimit" });
        var flow = new PurchaseWorkflowDefinition { OrganizationId = organizationId, Name = input.Name!.Trim(),
            FinanceThreshold = input.FinanceThreshold!.Value, GraphJson = input.Graph?.Serialize() };
        db.PurchaseWorkflows.Add(flow);
        await db.SaveChangesAsync(token);
        await transaction.CommitAsync(token);
        return Results.Created($"/api/organizations/{organizationId}/workflows/{flow.Id}", new { flow.Id });
    }

    private static Task<IResult> SaveDraftAsync(Guid organizationId, Guid workflowId, WorkflowDraft input, ClaimsPrincipal user, FlowOpsDb db, CancellationToken token) =>
        ChangeAsync(organizationId, workflowId, input.Revision, user, db, token, flow =>
        {
            if (!IsValid(input)) return Results.BadRequest(new { code = "invalidDraft" });
            var name = input.Name!.Trim();
            var threshold = input.FinanceThreshold!.Value;
            var graph = input.Graph?.Serialize();
            var graphChanged = graph is not null && graph != PurchaseGraph.Read(flow.GraphJson).Serialize();
            if (flow.Name != name || flow.FinanceThreshold != threshold || graphChanged)
            {
                flow.Name = name;
                flow.FinanceThreshold = threshold;
                if (graphChanged) flow.GraphJson = graph;
                flow.Revision++;
            }
            return null;
        });

    private static Task<IResult> PublishAsync(Guid organizationId, Guid workflowId, WorkflowChange input, ClaimsPrincipal user, FlowOpsDb db,
        IWorkflowDefinitionPublisher publisher, IWorkflowDefinitionStore store, CancellationToken token) =>
        ChangeCoreAsync(organizationId, workflowId, input.Revision, user, db, token, async flow =>
        {
            var graph = PurchaseGraph.Read(flow.GraphJson);
            if (!graph.CanPublish()) return Results.BadRequest(new { code = "invalidGraph" });
            if (flow.PublishedRevision == flow.Revision) return null;
            var number = (flow.PublishedVersion ?? 0) + 1;
            var definitionId = await PurchaseGraphCompiler.PublishAsync(flow, graph, number, publisher, store, token);
            db.PurchaseWorkflowVersions.Add(new PurchaseWorkflowVersion { OrganizationId = organizationId, WorkflowId = workflowId,
                Number = number, Name = flow.Name, FinanceThreshold = flow.FinanceThreshold, GraphJson = graph.Serialize(),
                ElsaVersion = PurchaseApprovalWorkflow.EngineVersion, ElsaDefinitionId = definitionId });
            flow.PublishedVersion = number;
            flow.PublishedRevision = flow.Revision;
            flow.Unpublished = false;
            return null;
        });

    private static Task<IResult> UnpublishAsync(Guid organizationId, Guid workflowId, WorkflowChange input, ClaimsPrincipal user, FlowOpsDb db, CancellationToken token) =>
        ChangeAsync(organizationId, workflowId, input.Revision, user, db, token, flow =>
        {
            if (flow.PublishedVersion is null || flow.Unpublished)
                return Results.Conflict(new { code = "workflowChanged" });
            flow.Unpublished = true;
            flow.Revision++;
            return null;
        });

    internal static async Task<IResult> DuplicateAsync(Guid organizationId, Guid workflowId, WorkflowCopy input, ClaimsPrincipal user,
        FlowOpsDb db, PlanEntitlements entitlements, CancellationToken token)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(token);
        if (await OrganizationEndpoints.LockAsync(db, organizationId, token) is not { } organization ||
            !CanManage(await MemberAsync(db, organizationId, user, token))) return Results.NotFound();
        var source = await db.PurchaseWorkflows.AsNoTracking()
            .SingleOrDefaultAsync(x => x.OrganizationId == organizationId && x.Id == workflowId, token);
        if (source is null) return Results.NotFound();
        if (source.Revision != input.Revision) return Results.Conflict(new { code = "workflowChanged" });
        var name = input.Name?.Trim();
        if (name is not { Length: >= 3 and <= 120 }) return Results.BadRequest(new { code = "invalidDraft" });
        if (!await entitlements.CanAddWorkflowAsync(db, organization, token))
            return Results.Conflict(new { code = "workflowPlanLimit" });
        var copy = new PurchaseWorkflowDefinition { OrganizationId = organizationId, Name = name, FinanceThreshold = source.FinanceThreshold,
            GraphJson = source.GraphJson };
        db.PurchaseWorkflows.Add(copy);
        await db.SaveChangesAsync(token);
        await transaction.CommitAsync(token);
        return Results.Created($"/api/organizations/{organizationId}/workflows/{copy.Id}", new { copy.Id });
    }

    private static Task<IResult> ArchiveAsync(Guid organizationId, Guid workflowId, WorkflowChange input, ClaimsPrincipal user, FlowOpsDb db, CancellationToken token) =>
        ChangeAsync(organizationId, workflowId, input.Revision, user, db, token, flow => { flow.Archived = true; return null; });

    private static Task<IResult> ChangeAsync(Guid organizationId, Guid workflowId, int revision, ClaimsPrincipal user,
        FlowOpsDb db, CancellationToken token, Func<PurchaseWorkflowDefinition, IResult?> change) =>
        ChangeCoreAsync(organizationId, workflowId, revision, user, db, token, flow => Task.FromResult(change(flow)));

    private static async Task<IResult> ChangeCoreAsync(Guid organizationId, Guid workflowId, int revision, ClaimsPrincipal user,
        FlowOpsDb db, CancellationToken token, Func<PurchaseWorkflowDefinition, Task<IResult?>> change)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(token);
        if (await OrganizationEndpoints.LockAsync(db, organizationId, token) is null ||
            !CanManage(await MemberAsync(db, organizationId, user, token))) return Results.NotFound();
        var flow = await db.PurchaseWorkflows.SingleOrDefaultAsync(x => x.OrganizationId == organizationId && x.Id == workflowId, token);
        if (flow is null) return Results.NotFound();
        if (flow.Revision != revision || flow.Archived) return Results.Conflict(new { code = "workflowChanged" });
        var failure = await change(flow);
        if (failure is not null) return failure;
        await db.SaveChangesAsync(token);
        await transaction.CommitAsync(token);
        return Results.NoContent();
    }

    internal static Task<PurchaseWorkflowVersion?> PublishedAsync(FlowOpsDb db, Guid organizationId, Guid workflowId, CancellationToken token) =>
        (from flow in db.PurchaseWorkflows.AsNoTracking()
         join version in db.PurchaseWorkflowVersions.AsNoTracking()
             on new { flow.OrganizationId, WorkflowId = flow.Id, Number = flow.PublishedVersion }
             equals new { version.OrganizationId, version.WorkflowId, Number = (int?)version.Number }
         where flow.OrganizationId == organizationId && flow.Id == workflowId && !flow.Archived && !flow.Unpublished
         select version).SingleOrDefaultAsync(token);

    private static bool IsValid(WorkflowDraft input) => input.Name?.Trim().Length is >= 3 and <= 120 &&
        input.FinanceThreshold is decimal value && value is >= 0 and <= 999_999_999.99m && decimal.Round(value, 2) == value &&
        (input.Graph is null || input.Graph.HasValidShape());
}

internal sealed record WorkflowDraft(string? Name, decimal? FinanceThreshold, int Revision, PurchaseGraph? Graph = null);
internal sealed record WorkflowChange(int Revision);
internal sealed record WorkflowCopy(string? Name, int Revision);
