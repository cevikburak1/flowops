using System.Diagnostics;

namespace FlowOps.Api;

internal sealed record SimulationInput(int? Version, int? Revision, decimal Amount, string? Manager, string? Finance);
internal sealed record SimulationStep(string NodeId, string Kind, string? Branch, string? NextNodeId,
    string? Decision, decimal? ComparedAmount, decimal? Threshold, bool MockNotification, double DurationMs);
internal sealed record SimulationIssue(string Code, string? NodeId = null);
internal sealed record SimulationResult(string Outcome, SimulationStep[] Steps, SimulationIssue[] Issues, double DurationMs);

// The interpreter has no service provider, persistence, or activity adapters.
internal static class WorkflowSimulation
{
    private const int MaxSteps = 8;
    private static readonly TimeSpan MaxDuration = TimeSpan.FromSeconds(1);

    public static bool Valid(SimulationInput input) =>
        (input.Version is > 0 && input.Revision is null || input.Version is null && input.Revision is > 0) &&
        input.Amount is > 0 and <= 999_999_999.99m && decimal.Round(input.Amount, 2) == input.Amount &&
        input.Manager is "approved" or "rejected" && input.Finance is "approved" or "rejected";

    public static SimulationResult Run(PurchaseGraph graph, decimal threshold, SimulationInput input, CancellationToken token)
    {
        var elapsed = Stopwatch.StartNew();
        if (!Valid(input)) return new("invalid", [], [new("invalidInput")], elapsed.Elapsed.TotalMilliseconds);
        if (!graph.HasValidShape())
            return new("invalid", [], [new("unsupportedGraph")], elapsed.Elapsed.TotalMilliseconds);

        var nodes = graph.Nodes!.ToDictionary(node => node!.Id!, node => node!);
        var edges = graph.Edges!.ToLookup(edge => edge!.Source!, edge => edge!);
        var start = nodes.Values.SingleOrDefault(node => node.Kind == "start");
        if (!graph.CanPublish())
        {
            var visited = new HashSet<string>(StringComparer.Ordinal);
            var frontier = new Queue<string>();
            if (start is not null) frontier.Enqueue(start.Id!);
            while (frontier.TryDequeue(out var id))
                if (visited.Add(id))
                    foreach (var edge in edges[id]) frontier.Enqueue(edge.Target!);
            var issues = nodes.Keys.Where(id => !visited.Contains(id)).Select(id => new SimulationIssue("unreachable", id))
                .Prepend(new("invalidGraph")).ToArray();
            return new("invalid", [], issues, elapsed.Elapsed.TotalMilliseconds);
        }

        var trace = new List<SimulationStep>(MaxSteps);
        var current = start!;
        string? manager = null;
        string? finance = null;
        for (var i = 0; i < MaxSteps; i++)
        {
            token.ThrowIfCancellationRequested();
            if (elapsed.Elapsed >= MaxDuration)
                return new("failed", trace.ToArray(), [new("timeout", current.Id)], elapsed.Elapsed.TotalMilliseconds);
            var step = Stopwatch.GetTimestamp();
            var kind = current.Kind!;
            if (kind == "manager") manager = input.Manager;
            if (kind == "finance") finance = input.Finance;
            var branch = kind switch
            {
                "decision" => manager,
                "threshold" => manager == "approved" && input.Amount > threshold ? "above" : "below",
                _ => null
            };
            var next = edges[current.Id!].SingleOrDefault(edge => edge.Branch == branch)?.Target;
            trace.Add(new(current.Id!, kind, branch, next, kind == "manager" ? manager : kind == "finance" ? finance : null,
                kind == "threshold" ? input.Amount : null, kind == "threshold" ? threshold : null,
                kind == "notify", Stopwatch.GetElapsedTime(step).TotalMilliseconds));
            if (kind == "end")
                return new(manager == "rejected" || finance == "rejected" ? "rejected" : "approved",
                    trace.ToArray(), [], elapsed.Elapsed.TotalMilliseconds);
            current = nodes[next!];
        }
        return new("failed", trace.ToArray(), [new("stepLimit", current.Id)], elapsed.Elapsed.TotalMilliseconds);
    }
}

internal sealed class WorkflowSimulationRun
{
    public Guid OrganizationId { get; init; }
    public Guid WorkflowId { get; init; }
    public Guid Id { get; init; } = Guid.NewGuid();
    public required string Actor { get; init; }
    public Guid? ScenarioId { get; init; }
    public required string Snapshot { get; init; }
    public DateTimeOffset CreatedAt { get; init; } = DateTimeOffset.UtcNow;
}

internal sealed class WorkflowSimulationScenario
{
    public Guid OrganizationId { get; init; }
    public Guid WorkflowId { get; init; }
    public Guid Id { get; init; } = Guid.NewGuid();
    public required string Name { get; init; }
    public required string Input { get; init; }
    public DateTimeOffset CreatedAt { get; init; } = DateTimeOffset.UtcNow;
}
