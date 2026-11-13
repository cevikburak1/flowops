using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace FlowOps.Api;

internal sealed class FlowOpsDb(DbContextOptions<FlowOpsDb> options) : DbContext(options)
{
    public DbSet<Organization> Organizations => Set<Organization>();
    public DbSet<Membership> Memberships => Set<Membership>();
    public DbSet<Invitation> Invitations => Set<Invitation>();
    public DbSet<OrganizationEvent> OrganizationEvents => Set<OrganizationEvent>();
    public DbSet<Purchase> Purchases => Set<Purchase>();
    public DbSet<Approval> Approvals => Set<Approval>();
    public DbSet<AuditEvent> Audit => Set<AuditEvent>();
    public DbSet<Notification> Notifications => Set<Notification>();
    public DbSet<WorkflowCommand> WorkflowCommands => Set<WorkflowCommand>();
    public DbSet<PurchaseWorkflowDefinition> PurchaseWorkflows => Set<PurchaseWorkflowDefinition>();
    public DbSet<PurchaseWorkflowVersion> PurchaseWorkflowVersions => Set<PurchaseWorkflowVersion>();

    protected override void OnModelCreating(ModelBuilder model)
    {
        model.HasDefaultSchema("flowops");
        model.Entity<Organization>().HasIndex(x => x.Slug).IsUnique();
        model.Entity<Organization>().Property(x => x.FinanceThreshold).HasPrecision(14, 2);
        model.Entity<Membership>().HasKey(x => new { x.OrganizationId, x.Subject });
        model.Entity<Membership>().HasIndex(x => x.Subject);
        model.Entity<Membership>().HasOne<Organization>().WithMany().HasForeignKey(x => x.OrganizationId);
        model.Entity<Invitation>().HasIndex(x => x.TokenHash).IsUnique();
        model.Entity<Invitation>().HasIndex(x => new { x.OrganizationId, x.ExpiresAt });
        model.Entity<Invitation>().HasOne<Organization>().WithMany().HasForeignKey(x => x.OrganizationId);
        model.Entity<OrganizationEvent>().HasOne<Organization>().WithMany().HasForeignKey(x => x.OrganizationId);
        model.Entity<OrganizationEvent>().HasIndex(x => new { x.OrganizationId, x.At });
        model.Entity<Purchase>().HasKey(x => new { x.OrganizationId, x.Id });
        model.Entity<Purchase>().HasIndex(x => new { x.OrganizationId, x.Requester, x.CreatedAt });
        model.Entity<Purchase>().HasOne<Organization>().WithMany().HasForeignKey(x => x.OrganizationId);
        model.Entity<Purchase>().Property(x => x.Amount).HasPrecision(14, 2);
        model.Entity<PurchaseWorkflowDefinition>().HasKey(x => new { x.OrganizationId, x.Id });
        model.Entity<PurchaseWorkflowDefinition>().HasOne<Organization>().WithMany().HasForeignKey(x => x.OrganizationId);
        model.Entity<PurchaseWorkflowDefinition>().Property(x => x.FinanceThreshold).HasPrecision(14, 2);
        model.Entity<PurchaseWorkflowVersion>().HasKey(x => new { x.OrganizationId, x.WorkflowId, x.Number });
        model.Entity<PurchaseWorkflowVersion>().HasOne<PurchaseWorkflowDefinition>().WithMany()
            .HasForeignKey(x => new { x.OrganizationId, Id = x.WorkflowId });
        model.Entity<PurchaseWorkflowVersion>().Property(x => x.FinanceThreshold).HasPrecision(14, 2);
        model.Entity<Purchase>().HasOne<PurchaseWorkflowVersion>().WithMany()
            .HasForeignKey(x => new { x.OrganizationId, x.WorkflowId, x.WorkflowVersion }).OnDelete(DeleteBehavior.Restrict);
        model.Entity<Purchase>().HasIndex(x => new { x.OrganizationId, x.WorkflowId, x.CreatedAt });
        model.Entity<Approval>().HasKey(x => new { x.OrganizationId, x.PurchaseId, x.Stage });
        model.Entity<Approval>().HasOne<Purchase>().WithMany().HasForeignKey(x => new { x.OrganizationId, x.PurchaseId });
        model.Entity<Approval>().HasIndex(x => new { x.OrganizationId, x.Assignee, x.Decision });
        model.Entity<Approval>().Property(x => x.Decision).IsConcurrencyToken();
        model.Entity<AuditEvent>().HasOne<Purchase>().WithMany().HasForeignKey(x => new { x.OrganizationId, x.PurchaseId });
        model.Entity<AuditEvent>().HasIndex(x => new { x.OrganizationId, x.PurchaseId, x.At });
        model.Entity<Notification>().HasOne<Purchase>().WithMany().HasForeignKey(x => new { x.OrganizationId, x.PurchaseId });
        model.Entity<Notification>().HasIndex(x => new { x.OrganizationId, x.Recipient, x.CreatedAt });
        model.Entity<WorkflowCommand>().HasOne<Purchase>().WithMany().HasForeignKey(x => new { x.OrganizationId, x.PurchaseId });
        model.Entity<WorkflowCommand>().HasIndex(x => new { x.OrganizationId, x.PurchaseId, x.Stage }).IsUnique();
        model.Entity<WorkflowCommand>().HasIndex(x => new { x.CompletedAt, x.NextAttemptAt });
    }
}

internal sealed class Organization
{
    public Guid Id { get; init; }
    public required string Name { get; init; }
    public required string Slug { get; init; }
    public decimal FinanceThreshold { get; set; } = 5000;
    public string? FinanceApprover { get; set; }
    public string Currency { get; init; } = "USD";
}

internal sealed class Membership
{
    public Guid OrganizationId { get; init; }
    public required string Subject { get; init; }
    public required string Role { get; set; }
    public string? Manager { get; set; }
    public string? Email { get; init; }
}

internal sealed class Invitation
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public Guid OrganizationId { get; init; }
    public required string Email { get; init; }
    public required string Role { get; init; }
    public required string TokenHash { get; init; }
    public DateTimeOffset ExpiresAt { get; init; }
    public DateTimeOffset? AcceptedAt { get; set; }
    public DateTimeOffset? RevokedAt { get; set; }
    public string? AcceptedBy { get; set; }
}

internal sealed class OrganizationEvent
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public Guid OrganizationId { get; init; }
    public required string Actor { get; init; }
    public required string Action { get; init; }
    public string? Target { get; init; }
    public DateTimeOffset At { get; init; } = DateTimeOffset.UtcNow;
}

internal sealed class Purchase
{
    public Guid OrganizationId { get; init; }
    public Guid Id { get; init; }
    public required string Requester { get; init; }
    public required string Title { get; init; }
    public required string Description { get; init; }
    public required string Category { get; init; }
    public required string Currency { get; init; }
    public decimal Amount { get; init; }
    public required string Manager { get; init; }
    public string? Finance { get; init; }
    public Guid? WorkflowId { get; init; }
    public int? WorkflowVersion { get; init; }
    public string Status { get; set; } = "starting";
    public DateTimeOffset CreatedAt { get; init; } = DateTimeOffset.UtcNow;
}

internal sealed class Approval
{
    public Guid OrganizationId { get; init; }
    public Guid PurchaseId { get; init; }
    public int Stage { get; init; }
    public required string Assignee { get; init; }
    public required string BookmarkId { get; init; }
    public string? Decision { get; set; }
    public string? Comment { get; set; }
    public DateTimeOffset? DecidedAt { get; set; }
}

internal sealed class AuditEvent
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public Guid OrganizationId { get; init; }
    public Guid PurchaseId { get; init; }
    public required string Actor { get; init; }
    public required string Action { get; init; }
    public required string CorrelationId { get; init; }
    public DateTimeOffset At { get; init; } = DateTimeOffset.UtcNow;
}

internal sealed class Notification
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public Guid OrganizationId { get; init; }
    public Guid PurchaseId { get; init; }
    public required string Recipient { get; init; }
    public required string Kind { get; init; }
    public DateTimeOffset CreatedAt { get; init; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? ReadAt { get; set; }
}

internal sealed class WorkflowCommand
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public Guid OrganizationId { get; init; }
    public Guid PurchaseId { get; init; }
    public int Stage { get; init; }
    public DateTimeOffset NextAttemptAt { get; set; } = DateTimeOffset.UtcNow;
    public int Attempts { get; set; }
    public DateTimeOffset? CompletedAt { get; set; }
}

internal sealed class DatabaseHealth(IServiceScopeFactory scopes) : IHealthCheck
{
    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken)
    {
        await using var scope = scopes.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<FlowOpsDb>().Database.CanConnectAsync(cancellationToken)
            ? HealthCheckResult.Healthy()
            : HealthCheckResult.Unhealthy();
    }
}
