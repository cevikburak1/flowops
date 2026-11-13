using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace FlowOps.Api;

internal static class StudioFormEndpoints
{
    private static readonly JsonSerializerOptions Json = JsonSerializerOptions.Web;
    private const int PageSize = 10;

    internal static void Map(RouteGroupBuilder tenant)
    {
        var forms = tenant.MapGroup("/forms").WithTags("Studio forms");
        forms.MapGet("", ListAsync);
        forms.MapGet("/{formId:guid}", GetAsync);
        forms.MapGet("/{formId:guid}/versions/{version:int}", GetVersionAsync);
        forms.MapPost("", CreateAsync).WithMetadata(new RequestSizeLimitAttribute(128_000));
        forms.MapPost("/{formId:guid}/draft", SaveAsync).WithMetadata(new RequestSizeLimitAttribute(128_000));
        forms.MapPost("/{formId:guid}/publish", PublishAsync).WithMetadata(new RequestSizeLimitAttribute(4096));
    }

    private static async Task<bool> CanEdit(Guid org, ClaimsPrincipal user, FlowOpsDb db, CancellationToken token) =>
        (await ProcessPermissions.FindAsync(db, org, PurchaseEndpoints.Subject(user), token))?.Grant.Edit == true;

    private static async Task<IResult> GetVersionAsync(Guid organizationId, Guid formId, int version, ClaimsPrincipal user,
        FlowOpsDb db, CancellationToken token)
    {
        if (!await CanEdit(organizationId, user, db, token)) return Results.NotFound();
        var schema = await StudioFormVersion.ResolveAsync(db, organizationId, new(formId, version), token);
        return schema is null ? Results.NotFound() : Results.Ok(schema);
    }

    private static async Task<IResult> ListAsync(Guid organizationId, int? page, ClaimsPrincipal user, FlowOpsDb db, CancellationToken token)
    {
        if (!await CanEdit(organizationId, user, db, token)) return Results.NotFound();
        var current = Math.Clamp(page ?? 1, 1, 10000);
        var forms = await db.StudioForms.Where(x => x.OrganizationId == organizationId).OrderBy(x => x.Name).ThenBy(x => x.Id)
            .Skip((current - 1) * PageSize).Take(PageSize + 1)
            .Select(x => new { x.Id, x.Name, x.Version }).ToListAsync(token);
        return Results.Ok(new { Items = forms.Take(PageSize), Page = current, HasMore = forms.Count > PageSize });
    }

    private static async Task<IResult> GetAsync(Guid organizationId, Guid formId, int? page, ClaimsPrincipal user,
        FlowOpsDb db, CancellationToken token)
    {
        if (!await CanEdit(organizationId, user, db, token)) return Results.NotFound();
        var form = await db.StudioForms.FindAsync([organizationId, formId], token);
        if (form is null) return Results.NotFound();
        var current = Math.Clamp(page ?? 1, 1, 10000);
        var versions = await db.StudioFormVersions.Where(x => x.OrganizationId == organizationId && x.FormId == formId)
            .OrderByDescending(x => x.Number).Skip((current - 1) * PageSize).Take(PageSize + 1)
            .Select(x => new { x.Number, x.PublishedAt }).ToListAsync(token);
        return Results.Ok(new { form.Id, form.Revision, form.Version, form.PublishedRevision,
            Draft = JsonSerializer.Deserialize<StudioFormSchema>(form.Draft, Json),
            Versions = versions.Take(PageSize), Page = current, HasMore = versions.Count > PageSize });
    }

    private static async Task<IResult> CreateAsync(Guid organizationId, StudioFormSchema input, ClaimsPrincipal user,
        FlowOpsDb db, CancellationToken token)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(token);
        if (await OrganizationEndpoints.LockAsync(db, organizationId, token) is null ||
            !await CanEdit(organizationId, user, db, token)) return Results.NotFound();
        if (!input.Valid()) return Invalid();
        var form = new StudioFormDefinition { OrganizationId = organizationId, Name = input.Name.Trim(),
            Draft = JsonSerializer.Serialize(input, Json) };
        db.StudioForms.Add(form);
        Audit(db, organizationId, user, "formCreated", form.Id);
        await db.SaveChangesAsync(token);
        await transaction.CommitAsync(token);
        return Results.Created($"/api/organizations/{organizationId}/forms/{form.Id}", new { form.Id });
    }

    private static Task<IResult> SaveAsync(Guid organizationId, Guid formId, FormChange input, ClaimsPrincipal user,
        FlowOpsDb db, CancellationToken token) => UpdateAsync(organizationId, formId, input, user, db, false, token);

    private static Task<IResult> PublishAsync(Guid organizationId, Guid formId, FormChange input, ClaimsPrincipal user,
        FlowOpsDb db, CancellationToken token) => UpdateAsync(organizationId, formId, input, user, db, true, token);

    private static async Task<IResult> UpdateAsync(Guid org, Guid formId, FormChange input, ClaimsPrincipal user,
        FlowOpsDb db, bool publish, CancellationToken token)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(token);
        if (await OrganizationEndpoints.LockAsync(db, org, token) is null || !await CanEdit(org, user, db, token))
            return Results.NotFound();
        var form = await db.StudioForms.FindAsync([org, formId], token);
        if (form is null) return Results.NotFound();
        if (form.Revision != input.Revision) return Results.Conflict(new { Code = "formChanged" });
        if (publish)
        {
            if (form.PublishedRevision == form.Revision) return Results.NoContent();
            if (JsonSerializer.Deserialize<StudioFormSchema>(form.Draft, Json)?.Fields.Length is not > 0) return Invalid();
            form.Version++;
            form.PublishedRevision = form.Revision;
            db.StudioFormVersions.Add(new() { OrganizationId = org, FormId = formId, Number = form.Version, Schema = form.Draft });
        }
        else
        {
            if (input.Schema is not { } schema || !schema.Valid()) return Invalid();
            var json = JsonSerializer.Serialize(schema, Json);
            if (form.Draft == json) return Results.Ok(new { form.Revision });
            form.Draft = json;
            form.Name = schema.Name.Trim();
            form.Revision++;
        }
        Audit(db, org, user, publish ? "formPublished" : "formSaved", formId);
        await db.SaveChangesAsync(token);
        await transaction.CommitAsync(token);
        return publish ? Results.NoContent() : Results.Ok(new { form.Revision });
    }

    private static IResult Invalid() => Results.BadRequest(new { Code = "invalidForm" });

    private static void Audit(FlowOpsDb db, Guid org, ClaimsPrincipal user, string action, Guid formId) =>
        db.OrganizationEvents.Add(new() { OrganizationId = org, Actor = PurchaseEndpoints.Subject(user), Action = action, Target = formId.ToString() });

    internal sealed record FormChange(int Revision, StudioFormSchema? Schema);
}
