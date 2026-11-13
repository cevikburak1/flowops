using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Testcontainers.PostgreSql;
using Xunit;

namespace FlowOps.Api.Tests;

public sealed class PostgresTests : IAsyncLifetime
{
    private readonly PostgreSqlContainer postgres = new PostgreSqlBuilder("postgres:16.14-alpine").Build();

    public async ValueTask InitializeAsync()
    {
        var token = TestContext.Current.CancellationToken;
        await postgres.StartAsync(token);
        await using var db = OpenDatabase();
        await db.Database.MigrateAsync(token);
    }

    public ValueTask DisposeAsync() => postgres.DisposeAsync();

    private FlowOpsDb OpenDatabase() => new(new DbContextOptionsBuilder<FlowOpsDb>()
        .UseNpgsql(postgres.GetConnectionString(), options => options.MigrationsHistoryTable("__EFMigrationsHistory", "flowops"))
        .Options);

    [Fact]
    public async Task WorkflowLimitSerializesConcurrentWritesAndIsTenantScoped()
    {
        var token = TestContext.Current.CancellationToken;
        var org = Guid.NewGuid();
        var other = Guid.NewGuid();
        var policy = new PlanEntitlements(new ConfigurationBuilder().AddInMemoryCollection(
            new Dictionary<string, string?> { ["Plans:starter:ActiveWorkflows"] = "1" }).Build());
        var owner = new ClaimsPrincipal(new ClaimsIdentity([new Claim("sub", "owner")], "test"));
        await using (var db = OpenDatabase())
        {
            db.Organizations.AddRange(
                new Organization { Id = org, Name = "First", Slug = "first" },
                new Organization { Id = other, Name = "Second", Slug = "second" });
            db.Memberships.AddRange(
                new Membership { OrganizationId = org, Subject = "owner", Role = "OrganizationOwner" },
                new Membership { OrganizationId = other, Subject = "owner", Role = "OrganizationOwner" });
            await db.SaveChangesAsync(token);
        }

        async Task<int?> Create(Guid tenant)
        {
            await using var db = OpenDatabase();
            var result = await WorkflowCatalog.CreateAsync(tenant, new WorkflowDraft("Test draft", 5000, 0), owner, db, policy, token);
            return Assert.IsAssignableFrom<IStatusCodeHttpResult>(result).StatusCode;
        }

        var results = await Task.WhenAll(Create(org), Create(org));
        Assert.Equal(new int?[] { 201, 409 }, results.Order());
        Assert.Equal(201, await Create(other));

        await using (var db = OpenDatabase())
        {
            var workflow = await db.PurchaseWorkflows.SingleAsync(x => x.OrganizationId == org, token);
            var copy = await WorkflowCatalog.DuplicateAsync(org, workflow.Id,
                new WorkflowCopy("Test copy", workflow.Revision), owner, db, policy, token);
            Assert.Equal(409, Assert.IsAssignableFrom<IStatusCodeHttpResult>(copy).StatusCode);
            workflow.Archived = true;
            await db.SaveChangesAsync(token);
        }
        Assert.Equal(201, await Create(org));
    }

    [Fact]
    public async Task InAppNotificationAndAllDeliveriesCommitTogether()
    {
        var token = TestContext.Current.CancellationToken;
        var organizationId = Guid.NewGuid();
        var purchaseId = Guid.NewGuid();
        var notice = new Notification
        {
            OrganizationId = organizationId, PurchaseId = purchaseId, Recipient = "reviewer", Kind = "approvalAssigned"
        };
        await using (var db = OpenDatabase())
        {
            db.Organizations.Add(new Organization { Id = organizationId, Name = "Outbox", Slug = "outbox" });
            db.Purchases.Add(new Purchase
            {
                OrganizationId = organizationId, Id = purchaseId, Requester = "requester", Manager = "reviewer",
                Title = "Test purchase", Description = "Outbox integration test", Category = "equipment",
                Currency = "USD", Amount = 10
            });
            db.Notifications.Add(notice);
            await db.SaveChangesAsync(token);
        }

        await using var read = OpenDatabase();
        Assert.Equal(organizationId, (await read.Notifications.SingleAsync(x => x.Id == notice.Id, token)).OrganizationId);
        var channels = await read.NotificationDeliveries.Where(x => x.NotificationId == notice.Id)
            .OrderBy(x => x.Channel).Select(x => x.Channel).ToArrayAsync(token);
        Assert.Equal(new[] { "email", "rabbitmq", "realtime", "webhook" }, channels);
    }
}
