using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Testcontainers.PostgreSql;
using Xunit;

namespace FlowOps.Api.Tests;

public sealed class PostgresTests : IAsyncLifetime
{
    [Fact]
    public async Task CopilotSavedQuestionsArePrivateBoundedAndCanBeCleared()
    {
        var token = TestContext.Current.CancellationToken;
        var org = Guid.NewGuid();
        var flow = new PurchaseWorkflowDefinition { OrganizationId = org, Name = "History workflow" };
        await using var db = OpenDatabase();
        db.Organizations.Add(new() { Id = org, Name = "History", Slug = "history" });
        db.Memberships.AddRange(new() { OrganizationId = org, Subject = "owner", Role = "OrganizationOwner" },
            new() { OrganizationId = org, Subject = "admin", Role = "OrganizationAdmin" });
        db.PurchaseWorkflows.Add(flow);
        await db.SaveChangesAsync(token);
        var owner = new ClaimsPrincipal(new ClaimsIdentity([new Claim("sub", "owner")], "test"));
        var admin = new ClaimsPrincipal(new ClaimsIdentity([new Claim("sub", "admin")], "test"));
        var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
            { ["Copilot:HistoryDays"] = "7" }).Build();
        var now = DateTimeOffset.UtcNow;
        db.CopilotQuestions.AddRange(Enumerable.Range(0, 21).Select(index => new CopilotQuestion
        {
            OrganizationId = org, WorkflowId = flow.Id, Subject = "owner", Text = $"Question number {index}",
            CreatedAt = now.AddMinutes(-index), ExpiresAt = index == 0 ? now.AddDays(-1) : now.AddDays(1)
        }));
        await db.SaveChangesAsync(token);
        db.ChangeTracker.Clear();
        var saved = await CopilotHistory.SaveAsync(org, flow.Id, new("Explain this workflow", "Finance reviews above the threshold."), owner, db, config, token);
        Assert.Equal(204, Assert.IsAssignableFrom<IStatusCodeHttpResult>(saved).StatusCode);
        Assert.Equal(20, await db.CopilotQuestions.CountAsync(token));
        Assert.Equal("Finance reviews above the threshold.", await db.CopilotQuestions.Where(row =>
            row.Text == "Explain this workflow").Select(row => row.Answer).SingleAsync(token));
        var other = await CopilotHistory.ListAsync(org, flow.Id, admin, db, config, token);
        var value = Assert.IsAssignableFrom<IValueHttpResult>(other).Value;
        Assert.Empty(Assert.IsAssignableFrom<System.Collections.IEnumerable>(value!.GetType().GetProperty("Items")!.GetValue(value)));
        var crossTenant = await CopilotHistory.ListAsync(Guid.NewGuid(), flow.Id, owner, db, config, token);
        Assert.Equal(404, Assert.IsAssignableFrom<IStatusCodeHttpResult>(crossTenant).StatusCode);
        await CopilotHistory.ClearAsync(org, flow.Id, admin, db, token);
        Assert.Equal(20, await db.CopilotQuestions.CountAsync(token));
        await CopilotHistory.ClearAsync(org, flow.Id, owner, db, token);
        Assert.Empty(await db.CopilotQuestions.ToListAsync(token));
    }

    [Fact]
    public async Task CopilotEnforcesTenantAndQuotaBeforeCallingProvider()
    {
        var token = TestContext.Current.CancellationToken;
        var org = Guid.NewGuid();
        await using var db = OpenDatabase();
        db.Organizations.Add(new() { Id = org, Name = "Copilot", Slug = "copilot" });
        db.Memberships.Add(new() { OrganizationId = org, Subject = "owner", Role = "OrganizationOwner" });
        await db.SaveChangesAsync(token);
        var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Copilot:Enabled"] = "true", ["Copilot:ApiKey"] = "unit-test-provider",
            ["Copilot:Model"] = "test-model", ["Copilot:MonthlyRequests"] = "1",
        }).Build();
        var user = new ClaimsPrincipal(new ClaimsIdentity([new Claim("sub", "owner")], "test"));
        var provider = new TestCopilotProvider();
        var prompt = new CopilotEndpoints.CopilotInput("Include finance above 5000");
        async Task<int?> Generate(Guid tenant, IConfiguration settings) =>
            Assert.IsAssignableFrom<IStatusCodeHttpResult>(
                await CopilotEndpoints.GenerateAsync(tenant, prompt, user, db, settings, provider, token)).StatusCode;

        Assert.Equal(404, await Generate(Guid.NewGuid(), config));
        Assert.Equal(503, await Generate(org, new ConfigurationBuilder().Build()));
        var analytics = await CopilotTools.AnalyticsAsync(org, new("Show average approval timing", 30),
            user, db, new ConfigurationBuilder().Build(), provider, token);
        Assert.Equal(503, Assert.IsAssignableFrom<IStatusCodeHttpResult>(analytics).StatusCode);
        Assert.Equal(0, provider.Calls);
        Assert.Equal(200, await Generate(org, config));
        Assert.Equal(429, await Generate(org, config));
        Assert.Equal(1, provider.Calls);
        var record = await db.CopilotUsages.SingleAsync(token);
        Assert.Equal("completed", record.Status);
        Assert.Equal(50, record.InputTokens);
        Assert.Null(record.EstimatedCostUsd);
        Assert.Empty(await db.PurchaseWorkflows.ToListAsync(token));
    }

    private sealed class TestCopilotProvider : ICopilotProvider
    {
        public int Calls { get; private set; }
        public Task<CopilotResult> GenerateAsync(string prompt, CancellationToken token, CopilotContract? contract = null)
        {
            Calls++;
            return Task.FromResult(new CopilotResult("""
                {"name":"Purchase review","route":"managerFinance","financeThreshold":5000,"limitations":[]}
                """, 50, 30, "completed"));
        }
    }

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

    [Fact]
    public async Task ParallelApprovalTransfersKeepQuorumDeadlineAndTenantScope()
    {
        var token = TestContext.Current.CancellationToken;
        var org = Guid.NewGuid();
        var other = Guid.NewGuid();
        var now = DateTimeOffset.UtcNow;
        var due = now.AddHours(3);
        var purchase = new Purchase { OrganizationId = org, Id = Guid.NewGuid(), Requester = "buyer", Manager = "first",
            Title = "Parallel fixture", Description = "Parallel fixture", Category = "equipment", Currency = "USD",
            Amount = 10, ReviewersJson = "[\"second\"]", MinimumApprovals = 2 };
        var status = (IResult result) => Assert.IsAssignableFrom<IStatusCodeHttpResult>(result).StatusCode;
        await using (var db = OpenDatabase())
        {
            db.Organizations.AddRange(new() { Id = org, Name = "Parallel", Slug = "parallel" },
                new() { Id = other, Name = "Other", Slug = "other" });
            db.Memberships.AddRange(new[] { "first", "second", "replacement" }.Select(subject =>
                new Membership { OrganizationId = org, Subject = subject, Role = "Manager" }));
            db.Memberships.Add(new() { OrganizationId = other, Subject = "outsider", Role = "Manager" });
            db.Purchases.Add(purchase);
            db.ApprovalDelegations.AddRange(new() { OrganizationId = org, Subject = "first", Delegate = "replacement",
                StartsAt = now.AddMinutes(-1), EndsAt = now.AddHours(1) }, new() { OrganizationId = org, Subject = "second",
                Delegate = "replacement", StartsAt = now.AddMinutes(-1), EndsAt = now.AddHours(1) });
            await db.SaveChangesAsync(token);
            Assert.Equal(new[] { ("first", "replacement"), ("second", "second") },
                await ApprovalDelegations.ResolveParallelAsync(db, purchase, 1, ["first", "second"], token));
            db.Approvals.Add(new() { OrganizationId = org, PurchaseId = purchase.Id, Stage = 1, Assignee = "first",
                BookmarkId = "bookmark", DueAt = due });
            db.ApprovalVotes.AddRange(new() { OrganizationId = org, PurchaseId = purchase.Id, Stage = 1, Subject = "first" },
                new() { OrganizationId = org, PurchaseId = purchase.Id, Stage = 1, Subject = "second" });
            await db.SaveChangesAsync(token);
        }
        var first = new ClaimsPrincipal(new ClaimsIdentity([new Claim("sub", "first")], "test"));
        async Task<int?> Transfer(string target, string expected)
        {
            await using var db = OpenDatabase();
            return status(await ApprovalAssignments.TransferAsync(org, purchase.Id, 1, new(target, expected), first, db, true, token));
        }
        Assert.Equal(404, await Transfer("replacement", "second"));
        Assert.Equal(400, await Transfer("second", "first"));
        Assert.Equal(400, await Transfer("outsider", "first"));
        Assert.Equal(204, await Transfer("replacement", "first"));
        Assert.Equal(409, await Transfer("replacement", "first"));
        await using var read = OpenDatabase();
        var approval = await read.Approvals.SingleAsync(token);
        Assert.Equal("replacement", approval.Assignee);
        Assert.Equal("bookmark", approval.BookmarkId);
        Assert.Equal(due.UtcTicks / 10, approval.DueAt.UtcTicks / 10);
        Assert.Equal(new[] { "replacement", "second" }, await read.ApprovalVotes.OrderBy(row => row.Subject)
            .Select(row => row.Subject).ToArrayAsync(token));
        Assert.Equal(2, (await read.Purchases.SingleAsync(token)).MinimumApprovals);
        Assert.Equal("replacement", (await read.Notifications.SingleAsync(token)).Recipient);
    }

    [Fact]
    public async Task EscalationRecoversFromPersistedScheduleAndDoesNotDuplicateDelivery()
    {
        var token = TestContext.Current.CancellationToken;
        var org = Guid.NewGuid();
        var purchase = Guid.NewGuid();
        await using (var db = OpenDatabase())
        {
            db.Organizations.Add(new() { Id = org, Name = "Escalation", Slug = "escalation" });
            db.Memberships.Add(new() { OrganizationId = org, Subject = "fallback", Role = "Manager" });
            db.Purchases.Add(new() { OrganizationId = org, Id = purchase, Requester = "buyer", Manager = "reviewer",
                Title = "Escalation fixture", Description = "Escalation fixture", Category = "equipment", Currency = "USD", Amount = 1 });
            db.Approvals.Add(new() { OrganizationId = org, PurchaseId = purchase, Stage = 1, Assignee = "reviewer",
                BookmarkId = "fixture", DueAt = DateTimeOffset.UtcNow.AddHours(1), WarnAt = DateTimeOffset.UtcNow.AddMinutes(-1),
                EscalationAction = "reassign", EscalationFallback = "fallback" });
            await db.SaveChangesAsync(token);
        }

        async Task Tick()
        {
            var registrations = new ServiceCollection();
            registrations.AddDbContext<FlowOpsDb>(options => options.UseNpgsql(postgres.GetConnectionString()));
            await using var provider = registrations.BuildServiceProvider();
            using var worker = new ApprovalAssignments(provider.GetRequiredService<IServiceScopeFactory>(), NullLogger<ApprovalAssignments>.Instance);
            await worker.EscalateAsync(token);
        }

        await Tick();
        await Tick();
        await using (var db = OpenDatabase())
        {
            var approval = await db.Approvals.SingleAsync(row => row.OrganizationId == org, token);
            Assert.NotNull(approval.WarnedAt);
            approval.DueAt = DateTimeOffset.UtcNow.AddMinutes(-1);
            await db.SaveChangesAsync(token);
        }
        await Tick();
        await Tick();
        await using var read = OpenDatabase();
        var escalated = await read.Approvals.SingleAsync(row => row.OrganizationId == org, token);
        Assert.Equal("fallback", escalated.Assignee);
        Assert.Equal("fixture", escalated.BookmarkId);
        Assert.NotNull(escalated.EscalatedAt);
        var kinds = await read.Notifications.Where(row => row.OrganizationId == org).Select(row => row.Kind).Order().ToArrayAsync(token);
        Assert.Equal(new[] { "approvalOverdue", "approvalReminder" }, kinds);
        Assert.Equal(8, await (from delivery in read.NotificationDeliveries join notice in read.Notifications
            on delivery.NotificationId equals notice.Id where notice.OrganizationId == org select delivery).CountAsync(token));
    }
}
