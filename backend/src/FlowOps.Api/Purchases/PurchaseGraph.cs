using System.Text.Json;

namespace FlowOps.Api;

internal sealed record PurchaseGraph(int SchemaVersion, PurchaseNode?[]? Nodes, PurchaseEdge?[]? Edges,
    [property: System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingDefault)]
    bool UseApprovalMatrix = false)
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    public static PurchaseGraph Default { get; } = new(1,
        [new("start", "start", 0, 110), new("manager", "manager", 210, 110),
         new("threshold", "threshold", 420, 110), new("finance", "finance", 630, 0), new("end", "end", 840, 110)],
        [new("start", "manager"), new("manager", "threshold"), new("threshold", "end", "below"),
         new("threshold", "finance", "above"), new("finance", "end")]);

    public static PurchaseGraph Read(string? json) => json is null ? Default : JsonSerializer.Deserialize<PurchaseGraph>(json, Json)!;

    public string Serialize() => JsonSerializer.Serialize(this, Json);

    public bool HasValidShape()
    {
        if (SchemaVersion != 1 || Nodes is not { Length: <= 7 } || Edges is not { Length: <= 8 }) return false;
        var ids = new HashSet<string>(StringComparer.Ordinal);
        var kinds = new HashSet<string>(StringComparer.Ordinal);
        foreach (var node in Nodes)
        {
            if (node?.Id is not { Length: > 0 and <= 48 } id ||
                id.Any(c => !char.IsAsciiLetterOrDigit(c) && c is not ('-' or '_')) ||
                node.Kind is not ("start" or "manager" or "threshold" or "finance" or "end" or "decision" or "notify") ||
                !double.IsFinite(node.X) || !double.IsFinite(node.Y) || Math.Abs(node.X) > 10000 || Math.Abs(node.Y) > 10000 ||
                !ids.Add(id) || !kinds.Add(node.Kind)) return false;
        }
        var links = new HashSet<PurchaseEdge>();
        foreach (var edge in Edges)
        {
            if (edge?.Source is not string source || edge.Target is not string target || source == target ||
                !ids.Contains(source) || !ids.Contains(target) ||
                edge.Branch is not (null or "above" or "below" or "approved" or "rejected") || !links.Add(edge)) return false;
        }
        return true;
    }

    public bool CanPublish()
    {
        if (!HasValidShape()) return false;
        var nodes = Nodes!.ToDictionary(node => node!.Id!, node => node!.Kind!);
        var start = Nodes!.SingleOrDefault(node => node!.Kind == "start");
        if (start is null || !nodes.ContainsValue("manager") || !nodes.ContainsValue("end") ||
            nodes.ContainsValue("threshold") != nodes.ContainsValue("finance")) return false;

        var outgoing = Edges!.ToLookup(edge => edge!.Source!, edge => edge!);
        var visited = new HashSet<string>();
        var path = new HashSet<string>();
        return Walk(start.Id!, false, false, false) && visited.Count == nodes.Count;

        bool Walk(string id, bool manager, bool aboveThreshold, bool rejected)
        {
            if (!path.Add(id)) return false;
            visited.Add(id);
            var kind = nodes[id];
            var links = outgoing[id].ToArray();
            var valid = kind switch
            {
                "end" => manager && links.Length == 0,
                "finance" => manager && aboveThreshold && !rejected && IsLinear(),
                "threshold" => manager && !rejected && HasBranches("above", "below") &&
                    nodes[links.Single(edge => edge.Branch == "above").Target!] == "finance",
                "decision" => manager && HasBranches("approved", "rejected"),
                _ => IsLinear(),
            };
            var result = valid && links.All(edge => Walk(edge.Target!, manager || kind == "manager",
                aboveThreshold || edge.Branch == "above", rejected || edge.Branch == "rejected"));
            path.Remove(id);
            return result;

            bool IsLinear() => links is [{ Branch: null }];
            bool HasBranches(string first, string second) => links.Length == 2 &&
                links.Any(edge => edge.Branch == first) && links.Any(edge => edge.Branch == second);
        }
    }
}

internal sealed record PurchaseNode(string? Id, string? Kind, double X, double Y);
internal sealed record PurchaseEdge(string? Source, string? Target, string? Branch = null);
