using Microsoft.EntityFrameworkCore;

namespace FlowOps.Api;

internal static class DevelopmentSeed
{
    private const string Employee = "92d58048-838b-4fc3-8ff8-aa8c48b86dac";
    private const string Manager = "fba63122-ae97-4cd4-b5ac-58d8087cdd18";
    private const string Finance = "b4fc7d75-09a6-46fb-8fb2-fcd4dbc74af4";

    public static async Task ApplyAsync(FlowOpsDb db)
    {
        var studio = Guid.Parse("14e41a68-86a8-4653-980c-07e1ec2b0562");
        var lab = Guid.Parse("17213e02-c104-49cf-93b5-a625628e5906");
        if (await db.Organizations.AnyAsync(x => x.Id == studio)) return;
        db.Organizations.AddRange(
            new Organization { Id = studio, Name = "Northstar Operations", Slug = "northstar" },
            new Organization { Id = lab, Name = "Juniper Labs", Slug = "juniper" });
        db.Memberships.AddRange(
            new Membership { OrganizationId = studio, Subject = Employee, Role = "Employee", Manager = Manager },
            new Membership { OrganizationId = studio, Subject = Manager, Role = "Manager" },
            new Membership { OrganizationId = studio, Subject = Finance, Role = "Approver" },
            new Membership { OrganizationId = lab, Subject = Finance, Role = "OrganizationOwner" });
        await db.SaveChangesAsync();
    }
}
