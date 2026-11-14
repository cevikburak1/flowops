using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace FlowOps.Api;

internal sealed class FlowOpsDb(DbContextOptions<FlowOpsDb> options) : DbContext(options)
{
    public DbSet<Organization> Organizations => Set<Organization>();
    public DbSet<CopilotUsage> CopilotUsages => Set<CopilotUsage>();
    public DbSet<CopilotQuestion> CopilotQuestions => Set<CopilotQuestion>();
    public DbSet<Membership> Memberships => Set<Membership>();
    public DbSet<OrganizationGroup> OrganizationGroups => Set<OrganizationGroup>();
    public DbSet<ApprovalAssignment> ApprovalAssignments => Set<ApprovalAssignment>();
    public DbSet<Invitation> Invitations => Set<Invitation>();
    public DbSet<OrganizationEvent> OrganizationEvents => Set<OrganizationEvent>();
    public DbSet<Purchase> Purchases => Set<Purchase>();
    public DbSet<Approval> Approvals => Set<Approval>();
    public DbSet<ApprovalVote> ApprovalVotes => Set<ApprovalVote>();
    public DbSet<ApprovalDelegation> ApprovalDelegations => Set<ApprovalDelegation>();
    public DbSet<AuditEvent> Audit => Set<AuditEvent>();
    public DbSet<Notification> Notifications => Set<Notification>();
    public DbSet<NotificationDelivery> NotificationDeliveries => Set<NotificationDelivery>();
    public DbSet<WorkflowCommand> WorkflowCommands => Set<WorkflowCommand>();
    public DbSet<PurchaseWorkflowDefinition> PurchaseWorkflows => Set<PurchaseWorkflowDefinition>();
    public DbSet<PurchaseWorkflowVersion> PurchaseWorkflowVersions => Set<PurchaseWorkflowVersion>();
    public DbSet<WorkflowSimulationRun> WorkflowSimulationRuns => Set<WorkflowSimulationRun>();
    public DbSet<WorkflowSimulationScenario> WorkflowSimulationScenarios => Set<WorkflowSimulationScenario>();
    public DbSet<StudioFormDefinition> StudioForms => Set<StudioFormDefinition>();
    public DbSet<StudioFormVersion> StudioFormVersions => Set<StudioFormVersion>();
    public DbSet<StudioSubmissionDraft> StudioSubmissionDrafts => Set<StudioSubmissionDraft>();
    public DbSet<ProcessDefinition> Processes => Set<ProcessDefinition>();
    public DbSet<ProcessRun> ProcessRuns => Set<ProcessRun>();
    public DbSet<ProcessAttachment> ProcessAttachments => Set<ProcessAttachment>();
    public DbSet<ProcessTask> ProcessTasks => Set<ProcessTask>();

    protected override void OnModelCreating(ModelBuilder model)
    {
        model.HasDefaultSchema("flowops");
        model.Entity<CopilotUsage>().HasOne<Organization>().WithMany().HasForeignKey(row => row.OrganizationId);
        model.Entity<CopilotUsage>().HasIndex(row => new { row.OrganizationId, row.At });
        model.Entity<CopilotUsage>().HasIndex(row => new { row.OrganizationId, row.Subject, row.At });
        model.Entity<CopilotQuestion>().HasOne<PurchaseWorkflowDefinition>().WithMany()
            .HasForeignKey(row => new { row.OrganizationId, Id = row.WorkflowId });
        model.Entity<CopilotQuestion>().HasOne<Membership>().WithMany()
            .HasForeignKey(row => new { row.OrganizationId, row.Subject });
        model.Entity<CopilotQuestion>().HasIndex(row => new { row.OrganizationId, row.WorkflowId, row.Subject, row.CreatedAt });
        model.Entity<CopilotQuestion>().HasIndex(row => row.ExpiresAt);
        model.Entity<StudioFormDefinition>().HasKey(x => new { x.OrganizationId, x.Id });
        model.Entity<StudioFormDefinition>().HasOne<Organization>().WithMany().HasForeignKey(x => x.OrganizationId);
        model.Entity<StudioFormDefinition>().HasIndex(x => new { x.OrganizationId, x.Name });
        model.Entity<StudioFormVersion>().HasKey(x => new { x.OrganizationId, x.FormId, x.Number });
        model.Entity<StudioFormVersion>().HasOne<StudioFormDefinition>().WithMany()
            .HasForeignKey(x => new { x.OrganizationId, Id = x.FormId });
        model.Entity<StudioSubmissionDraft>().HasKey(x => new { x.OrganizationId, x.ProcessId, x.Subject, x.ProcessVersion });
        model.Entity<StudioSubmissionDraft>().HasOne<ProcessDefinition>().WithMany()
            .HasForeignKey(x => new { x.OrganizationId, Id = x.ProcessId });
        model.Entity<WorkflowSimulationScenario>().HasKey(x => new { x.OrganizationId, x.WorkflowId, x.Id });
        model.Entity<WorkflowSimulationRun>().HasKey(x => new { x.OrganizationId, x.WorkflowId, x.Id });
        model.Entity<WorkflowSimulationScenario>().HasOne<PurchaseWorkflowDefinition>().WithMany()
            .HasForeignKey(x => new { x.OrganizationId, Id = x.WorkflowId });
        model.Entity<WorkflowSimulationRun>().HasOne<PurchaseWorkflowDefinition>().WithMany()
            .HasForeignKey(x => new { x.OrganizationId, Id = x.WorkflowId });
        model.Entity<WorkflowSimulationRun>().HasOne<WorkflowSimulationScenario>().WithMany()
            .HasForeignKey(x => new { x.OrganizationId, x.WorkflowId, Id = x.ScenarioId }).OnDelete(DeleteBehavior.NoAction);
        model.Entity<WorkflowSimulationScenario>().HasIndex(x => new { x.OrganizationId, x.WorkflowId, x.CreatedAt });
        model.Entity<WorkflowSimulationRun>().HasIndex(x => new { x.OrganizationId, x.WorkflowId, x.CreatedAt });
        model.Entity<ProcessDefinition>().HasKey(x => new { x.OrganizationId, x.Id });
        model.Entity<ProcessDefinition>().HasOne<Organization>().WithMany().HasForeignKey(x => x.OrganizationId);
        model.Entity<ProcessRun>().HasKey(x => new { x.OrganizationId, x.Id });
        model.Entity<ProcessRun>().HasOne<ProcessDefinition>().WithMany()
            .HasForeignKey(x => new { x.OrganizationId, Id = x.ProcessId }).OnDelete(DeleteBehavior.Restrict);
        model.Entity<ProcessRun>().HasIndex(x => x.NextAt).HasFilter("\"Status\" = 'running'");
        model.Entity<ProcessRun>().HasIndex(x => new { x.OrganizationId, x.ProcessId, x.CreatedAt });
        model.Entity<ProcessAttachment>().HasKey(x => new { x.OrganizationId, x.RunId });
        model.Entity<ProcessAttachment>().HasOne<ProcessRun>().WithMany()
            .HasForeignKey(x => new { x.OrganizationId, Id = x.RunId });
        model.Entity<ProcessTask>().HasKey(x => new { x.OrganizationId, x.RunId, x.StepId });
        model.Entity<ProcessTask>().HasOne<ProcessRun>().WithMany()
            .HasForeignKey(x => new { x.OrganizationId, Id = x.RunId });
        model.Entity<ProcessTask>().HasIndex(x => new { x.OrganizationId, x.Assignee, x.CompletedAt });
        model.Entity<Organization>().HasIndex(x => x.Slug).IsUnique();
        model.Entity<Organization>().Property(x => x.FinanceThreshold).HasPrecision(14, 2);
        model.Entity<Organization>().Property(x => x.Plan).HasDefaultValue("starter");
        model.Entity<Membership>().HasKey(x => new { x.OrganizationId, x.Subject });
        model.Entity<Membership>().HasIndex(x => x.Subject);
        model.Entity<Membership>().HasOne<Organization>().WithMany().HasForeignKey(x => x.OrganizationId);
        model.Entity<OrganizationGroup>().HasKey(x => new { x.OrganizationId, x.Id });
        model.Entity<OrganizationGroup>().HasOne<Organization>().WithMany().HasForeignKey(x => x.OrganizationId);
        model.Entity<OrganizationGroup>().HasIndex(x => new { x.OrganizationId, x.Kind, x.Name }).IsUnique();
        model.Entity<Membership>().HasOne<OrganizationGroup>().WithMany()
            .HasForeignKey(x => new { x.OrganizationId, Id = x.TeamId }).OnDelete(DeleteBehavior.Restrict);
        model.Entity<Membership>().HasOne<OrganizationGroup>().WithMany()
            .HasForeignKey(x => new { x.OrganizationId, Id = x.DepartmentId }).OnDelete(DeleteBehavior.Restrict);
        model.Entity<Invitation>().HasIndex(x => x.TokenHash).IsUnique();
        model.Entity<Invitation>().HasIndex(x => new { x.OrganizationId, x.ExpiresAt });
        model.Entity<Invitation>().HasIndex(x => x.EmailNextAttemptAt).HasFilter("\"EmailCode\" IS NOT NULL");
        model.Entity<Invitation>().HasOne<Organization>().WithMany().HasForeignKey(x => x.OrganizationId);
        model.Entity<OrganizationEvent>().HasOne<Organization>().WithMany().HasForeignKey(x => x.OrganizationId);
        model.Entity<OrganizationEvent>().HasIndex(x => new { x.OrganizationId, x.At });
        model.Entity<Purchase>().HasKey(x => new { x.OrganizationId, x.Id });
        model.Entity<Purchase>().HasIndex(x => new { x.OrganizationId, x.Requester, x.CreatedAt });
        model.Entity<Purchase>().HasIndex(x => new { x.OrganizationId, x.CreatedAt });
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
        model.Entity<Purchase>().HasIndex(x => new { x.OrganizationId, x.PreviousPurchaseId }).IsUnique();
        model.Entity<Purchase>().HasOne<Purchase>().WithMany().HasForeignKey(x => new { x.OrganizationId, Id = x.PreviousPurchaseId })
            .OnDelete(DeleteBehavior.Restrict);
        model.Entity<ApprovalVote>().HasKey(x => new { x.OrganizationId, x.PurchaseId, x.Stage, x.Subject });
        model.Entity<ApprovalDelegation>().HasKey(x => new { x.OrganizationId, x.Subject });
        model.Entity<ApprovalDelegation>().HasOne<Membership>().WithMany().HasForeignKey(x => new { x.OrganizationId, x.Subject });
        model.Entity<ApprovalDelegation>().HasOne<Membership>().WithMany()
            .HasForeignKey(x => new { x.OrganizationId, Subject = x.Delegate });
        model.Entity<ApprovalVote>().HasOne<Approval>().WithMany().HasForeignKey(x => new { x.OrganizationId, x.PurchaseId, x.Stage });
        model.Entity<ApprovalVote>().HasIndex(x => new { x.OrganizationId, x.Subject, x.Decision });
        model.Entity<Approval>().HasOne<Purchase>().WithMany().HasForeignKey(x => new { x.OrganizationId, x.PurchaseId });
        model.Entity<Approval>().HasIndex(x => new { x.OrganizationId, x.Assignee, x.Decision });
        model.Entity<Approval>().HasIndex(x => new { x.OrganizationId, x.Assignee, x.Decision, x.DueAt });
        model.Entity<Approval>().Property(x => x.Decision).IsConcurrencyToken();
        model.Entity<Approval>().Property(x => x.Assignee).IsConcurrencyToken();
        model.Entity<Approval>().HasIndex(x => x.DueAt).HasFilter("\"Decision\" IS NULL AND \"EscalatedAt\" IS NULL");
        model.Entity<Approval>().HasIndex(x => x.WarnAt).HasFilter("\"Decision\" IS NULL AND \"WarnedAt\" IS NULL AND \"WarnAt\" IS NOT NULL");
        model.Entity<ApprovalAssignment>().HasOne<Approval>().WithMany()
            .HasForeignKey(x => new { x.OrganizationId, x.PurchaseId, x.Stage });
        model.Entity<ApprovalAssignment>().HasIndex(x => new { x.OrganizationId, x.PurchaseId, x.Stage, x.At });
        model.Entity<AuditEvent>().HasOne<Purchase>().WithMany().HasForeignKey(x => new { x.OrganizationId, x.PurchaseId });
        model.Entity<AuditEvent>().HasIndex(x => new { x.OrganizationId, x.PurchaseId, x.At });
        model.Entity<AuditEvent>().HasIndex(x => new { x.OrganizationId, x.At });
        model.Entity<Notification>().HasOne<Purchase>().WithMany().HasForeignKey(x => new { x.OrganizationId, x.PurchaseId });
        model.Entity<Notification>().HasOne<ProcessRun>().WithMany()
            .HasForeignKey(x => new { x.OrganizationId, Id = x.ProcessRunId });
        model.Entity<Notification>().HasIndex(x => new { x.OrganizationId, x.Recipient, x.CreatedAt });
        model.Entity<NotificationDelivery>().HasKey(x => new { x.NotificationId, x.Channel });
        model.Entity<NotificationDelivery>().HasOne<Notification>().WithMany().HasForeignKey(x => x.NotificationId);
        model.Entity<NotificationDelivery>().HasIndex(x => new { x.Channel, x.NextAttemptAt }).HasFilter("\"CompletedAt\" IS NULL");
        model.Entity<WorkflowCommand>().HasOne<Purchase>().WithMany().HasForeignKey(x => new { x.OrganizationId, x.PurchaseId });
        model.Entity<WorkflowCommand>().HasIndex(x => new { x.OrganizationId, x.PurchaseId, x.Stage }).IsUnique();
        model.Entity<WorkflowCommand>().HasIndex(x => new { x.CompletedAt, x.NextAttemptAt });
    }

    public override Task<int> SaveChangesAsync(bool acceptAllChangesOnSuccess, CancellationToken cancellationToken = default)
    {
        var notifications = ChangeTracker.Entries<Notification>().Where(x => x.State == EntityState.Added)
            .Select(x => x.Entity.Id).ToArray();
        foreach (var id in notifications)
        {
            foreach (var channel in NotificationDispatcher.Channels)
                if (NotificationDeliveries.Local.All(x => x.NotificationId != id || x.Channel != channel))
                    NotificationDeliveries.Add(new NotificationDelivery { NotificationId = id, Channel = channel });
        }
        return base.SaveChangesAsync(acceptAllChangesOnSuccess, cancellationToken);
    }
}

internal sealed class Organization
{
    public string? ApprovalMatrixJson { get; set; }
    public int ApprovalMatrixRevision { get; set; }
    public string? ApprovalSlaJson { get; set; }
    public int ApprovalSlaRevision { get; set; }
    public Guid Id { get; init; }
    public required string Name { get; init; }
    public required string Slug { get; init; }
    public decimal FinanceThreshold { get; set; } = 5000;
    public string? FinanceApprover { get; set; }
    public string Currency { get; init; } = "USD";
    public bool WebhookEnabled { get; set; }
    public string Plan { get; set; } = "starter";
    public string? ProcessPermissions { get; set; }
}

internal sealed class Membership
{
    public Guid OrganizationId { get; init; }
    public required string Subject { get; init; }
    public required string Role { get; set; }
    public string? Manager { get; set; }
    public Guid? TeamId { get; set; }
    public Guid? DepartmentId { get; set; }
    public string? Email { get; init; }
}

internal sealed class OrganizationGroup
{
    public Guid OrganizationId { get; init; }
    public Guid Id { get; init; } = Guid.NewGuid();
    public required string Kind { get; init; }
    public required string Name { get; set; }
    public string? Lead { get; set; }
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
    public string? EmailCode { get; set; }
    public DateTimeOffset? EmailNextAttemptAt { get; set; }
    public int EmailAttempts { get; set; }
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
    public Guid? PreviousPurchaseId { get; init; }
    public DateTimeOffset? WorkflowDueAt { get; init; }
    public string? ReviewersJson { get; init; }
    public int? MinimumApprovals { get; init; }
    public Guid OrganizationId { get; init; }
    public Guid Id { get; init; }
    public required string Requester { get; init; }
    public required string Title { get; init; }
    public required string Description { get; init; }
    public required string Category { get; init; }
    public required string Currency { get; init; }
    public decimal Amount { get; init; }
    public string Priority { get; init; } = "normal";
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
    public required string Assignee { get; set; }
    public required string BookmarkId { get; init; }
    public DateTimeOffset DueAt { get; set; }
    public DateTimeOffset? WarnAt { get; set; }
    public DateTimeOffset? WarnedAt { get; set; }
    public string? EscalationAction { get; set; }
    public string? EscalationFallback { get; set; }
    public DateTimeOffset? EscalatedAt { get; set; }
    public string? Decision { get; set; }
    public string? Comment { get; set; }
    public DateTimeOffset? DecidedAt { get; set; }
}

internal sealed class ApprovalVote
{
    public Guid OrganizationId { get; init; }
    public Guid PurchaseId { get; init; }
    public int Stage { get; init; }
    public required string Subject { get; init; }
    public string? Decision { get; set; }
    public string? Comment { get; set; }
    public DateTimeOffset? DecidedAt { get; set; }
}

internal sealed class ApprovalAssignment
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public Guid OrganizationId { get; init; }
    public Guid PurchaseId { get; init; }
    public int Stage { get; init; }
    public required string Assignee { get; init; }
    public required string Actor { get; init; }
    public DateTimeOffset At { get; init; } = DateTimeOffset.UtcNow;
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
    public Guid? ProcessId { get; init; }
    public Guid? ProcessRunId { get; init; }
    public string? ProcessTitle { get; init; }
    public Guid Id { get; init; } = Guid.NewGuid();
    public Guid OrganizationId { get; init; }
    public Guid? PurchaseId { get; init; }
    public required string Recipient { get; init; }
    public required string Kind { get; init; }
    public DateTimeOffset CreatedAt { get; init; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? ReadAt { get; set; }
}

internal sealed class NotificationDelivery
{
    public Guid NotificationId { get; init; }
    public required string Channel { get; init; }
    public DateTimeOffset NextAttemptAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? CompletedAt { get; set; }
    public int Attempts { get; set; }
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
