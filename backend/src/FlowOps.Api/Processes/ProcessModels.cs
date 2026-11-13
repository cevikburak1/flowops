using System.Text.Json;

namespace FlowOps.Api;

internal sealed class ProcessDefinition
{
    public Guid OrganizationId { get; init; }
    public Guid Id { get; init; } = Guid.NewGuid();
    public required string Draft { get; set; }
    public int Revision { get; set; } = 1;
    public int Version { get; set; }
    public string? Published { get; set; }
    public string? ElsaDefinitionId { get; set; }
}

internal sealed class ProcessRun
{
    public Guid OrganizationId { get; init; }
    public Guid Id { get; init; }
    public Guid ProcessId { get; init; }
    public int Version { get; init; }
    public required string ElsaDefinitionId { get; init; }
    public required string Subject { get; init; }
    public required string Answers { get; init; }
    public required string Fingerprint { get; init; }
    public string Status { get; set; } = "running";
    public DateTimeOffset CreatedAt { get; init; } = DateTimeOffset.UtcNow;
    public DateTimeOffset NextAt { get; set; } = DateTimeOffset.UtcNow;
    public int Attempts { get; set; }
}

internal sealed class ProcessAttachment
{
    public Guid OrganizationId { get; init; }
    public Guid RunId { get; init; }
    public required string Name { get; init; }
    public byte[]? Content { get; init; }
    public string? ObjectKey { get; init; }
}

internal sealed class ProcessTask
{
    public Guid OrganizationId { get; init; }
    public Guid RunId { get; init; }
    public required string StepId { get; init; }
    public required string Assignee { get; init; }
    public required string Title { get; init; }
    public DateTimeOffset AssignedAt { get; init; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? CompletedAt { get; set; }
}

internal sealed record ProcessPosition(double X, double Y);

internal sealed record ProcessSpec(string? Name, string? Trigger, ProcessField?[]? Fields, ProcessStep?[]? Steps, string? Attachment,
    Dictionary<string, ProcessPosition?>? Layout = null)
{
    public static ProcessSpec Read(string json) => JsonSerializer.Deserialize<ProcessSpec>(json, JsonSerializerOptions.Web)!;
    public string Serialize() => JsonSerializer.Serialize(this, JsonSerializerOptions.Web);

    public bool Valid(ProcessTargets targets) => Name?.Trim().Length is >= 3 and <= 100 &&
        Trigger is "manual" or "webhook" && Attachment is "none" or "optional" or "required" &&
        Fields is { Length: <= 12 } && Fields.All(x => x is not null && x.Valid()) &&
        Fields.Select(x => x!.Key).Distinct(StringComparer.Ordinal).Count() == Fields.Length &&
        Steps is { Length: >= 1 and <= 12 } && Steps.All(x => x is not null && x.Valid(targets, true)) &&
        (Layout is null || Layout.Count <= 500 && Layout.All(entry => entry.Key.Length <= 64 &&
            entry.Value is { X: >= -10000 and <= 10000, Y: >= -10000 and <= 10000 }));

    public static IEnumerable<string> Assignees(IEnumerable<ProcessStep?> steps) =>
        steps.SelectMany(step => step?.Kind switch
        {
            "assignment" => new[] { step.Assignee! },
            "parallel" => step.Branches!.SelectMany(branch => Assignees(branch!)),
            _ => []
        });
}

internal sealed record ProcessField(string? Key, string? Label, string? Type, bool Required)
{
    public bool Valid() => Key is { Length: >= 1 and <= 32 } && Key.All(x => char.IsAsciiLetterOrDigit(x) || x == '_') &&
        Label?.Trim().Length is >= 1 and <= 80 && Type is "text" or "number" or "date";

    public bool ValidAnswer(string value) => value.Length <= 2000 && (value.Length == 0 ? !Required : Type switch
    {
        "number" => decimal.TryParse(value, System.Globalization.NumberStyles.AllowLeadingSign |
            System.Globalization.NumberStyles.AllowDecimalPoint, System.Globalization.CultureInfo.InvariantCulture, out _),
        "date" => DateOnly.TryParseExact(value, "yyyy-MM-dd", out _),
        _ => true
    });
}

internal sealed record ProcessStep(string? Kind, int? Seconds, string? Target, ProcessStep?[]?[]? Branches,
    string? Assignee = null, string? Title = null)
{
    public bool Valid(ProcessTargets targets, bool allowParallel) => Kind switch
    {
        "timer" => Seconds is >= 1 and <= 604800 && Branches is null && Target is null,
        "http" => Seconds is null && Branches is null && targets.Resolve(Target) is not null,
        "assignment" => Seconds is null && Target is null && Branches is null && Assignee is { Length: >= 1 and <= 255 } &&
            Title?.Trim().Length is >= 1 and <= 100,
        "parallel" => allowParallel && Seconds is null && Target is null && Branches is { Length: >= 2 and <= 4 } &&
            Branches.All(branch => branch is { Length: >= 1 and <= 8 } &&
                branch.All(step => step is not null && step.Valid(targets, false))),
        _ => false
    };
}
