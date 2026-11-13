using System.Security.Claims;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;

namespace FlowOps.Api;

internal sealed record ProcessGrant(bool Edit, bool Run, bool Inspect);
internal sealed record ProcessAccess(string Role, ProcessGrant Grant);

internal static class ProcessPermissions
{
    private static readonly Dictionary<string, ProcessGrant> Defaults = new(StringComparer.Ordinal)
    {
        ["ProcessEditor"] = new(true, true, false),
        ["ProcessOperator"] = new(false, true, false),
        ["Employee"] = new(false, true, false),
        ["Manager"] = new(false, true, false),
        ["Approver"] = new(false, true, false),
        ["Viewer"] = new(false, false, false)
    };

    public static ProcessGrant Resolve(string role, string? settings) => OrganizationEndpoints.CanManage(role)
        ? new(true, true, true)
        : Read(settings).GetValueOrDefault(role) ?? new(false, false, false);

    public static Dictionary<string, ProcessGrant> Read(string? settings)
    {
        var grants = new Dictionary<string, ProcessGrant>(Defaults, StringComparer.Ordinal);
        if (settings is not null)
            foreach (var (role, grant) in JsonSerializer.Deserialize<Dictionary<string, ProcessGrant>>(settings, JsonSerializerOptions.Web)!)
                grants[role] = grant;
        return grants;
    }

    public static async Task<ProcessAccess?> FindAsync(FlowOpsDb db, Guid org, string subject, CancellationToken token)
    {
        var data = await (from member in db.Memberships
            join organization in db.Organizations on member.OrganizationId equals organization.Id
            where member.OrganizationId == org && member.Subject == subject
            select new { member.Role, organization.ProcessPermissions }).SingleOrDefaultAsync(token);
        return data is null ? null : new ProcessAccess(data.Role, Resolve(data.Role, data.ProcessPermissions));
    }

    public static async Task<IResult> UpdateAsync(Guid organizationId, Dictionary<string, ProcessGrant?> grants,
        ClaimsPrincipal user, FlowOpsDb db, CancellationToken token)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(token);
        var organization = await OrganizationEndpoints.LockAsync(db, organizationId, token);
        var actor = await OrganizationEndpoints.FindAdminAsync(db, organizationId, user, token);
        if (organization is null || actor is null) return Results.NotFound();
        if (actor.Role != "OrganizationOwner") return Results.StatusCode(403);
        if (grants.Count != Defaults.Count || grants.Any(x => !Defaults.ContainsKey(x.Key) || x.Value is null))
            return Results.BadRequest(new { code = "invalidProcessPermissions" });
        var json = JsonSerializer.Serialize(new SortedDictionary<string, ProcessGrant?>(grants, StringComparer.Ordinal), JsonSerializerOptions.Web);
        if (organization.ProcessPermissions == json) return Results.NoContent();
        organization.ProcessPermissions = json;
        db.OrganizationEvents.Add(new OrganizationEvent { OrganizationId = organizationId,
            Actor = actor.Subject, Action = "processPermissionsUpdated" });
        await db.SaveChangesAsync(token);
        await transaction.CommitAsync(token);
        return Results.NoContent();
    }
}
