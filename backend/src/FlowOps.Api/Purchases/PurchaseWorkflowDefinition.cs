namespace FlowOps.Api;

internal sealed class PurchaseWorkflowDefinition
{
    public Guid OrganizationId { get; init; }
    public Guid Id { get; init; } = Guid.NewGuid();
    public required string Name { get; set; }
    public decimal FinanceThreshold { get; set; }
    public string? GraphJson { get; set; }
    public int Revision { get; set; } = 1;
    public int? PublishedVersion { get; set; }
    public int? PublishedRevision { get; set; }
    public bool Unpublished { get; set; }
    public bool Archived { get; set; }
}

internal sealed class PurchaseWorkflowVersion
{
    public Guid OrganizationId { get; init; }
    public Guid WorkflowId { get; init; }
    public int Number { get; init; }
    public required string Name { get; init; }
    public decimal FinanceThreshold { get; init; }
    public string? GraphJson { get; init; }
    public int ElsaVersion { get; init; }
    public string? ElsaDefinitionId { get; init; }
    public DateTimeOffset PublishedAt { get; init; } = DateTimeOffset.UtcNow;
}
