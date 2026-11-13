using Microsoft.EntityFrameworkCore;

namespace FlowOps.Api;

internal sealed class PlanEntitlements(IConfiguration configuration)
{
    internal static bool IsKnownPlan(string? plan) => plan is "starter" or "business" or "enterprise";

    // Call while holding the organization lock so concurrent writes share the same quota.
    public async Task<bool> CanAddWorkflowAsync(FlowOpsDb db, Organization organization, CancellationToken token)
    {
        var limit = configuration.GetValue<int?>($"Plans:{organization.Plan}:ActiveWorkflows");
        if (limit is null) return true;
        var count = await db.PurchaseWorkflows.CountAsync(x => x.OrganizationId == organization.Id && !x.Archived, token);
        return count < limit;
    }

    public void Validate()
    {
        foreach (var plan in configuration.GetSection("Plans").GetChildren())
        {
            if (!IsKnownPlan(plan.Key) || plan.GetValue<int?>("ActiveWorkflows") is < 0)
                throw new InvalidOperationException("Configure ActiveWorkflows with a nonnegative limit for a known plan.");
        }
    }
}
