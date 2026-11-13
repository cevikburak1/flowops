using System.Text.Json;
using System.Globalization;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;

namespace FlowOps.Api;

internal sealed record StudioField(string Key, string Label, string Type, bool Required, string? WhenKey = null,
    string? WhenValue = null, string[]? Options = null, string? Help = null, int? MaxLength = null,
    string? Placeholder = null, string? DefaultValue = null, decimal? Minimum = null, decimal? Maximum = null,
    int? MinLength = null, string? Pattern = null, bool HalfWidth = false,
    string? RequiredWhenKey = null, string? RequiredWhenValue = null, int Step = 1,
    int? FileLimitKiB = null, string[]? FileExtensions = null)
{
    public bool Valid() =>
        Key is { Length: >= 1 and <= 32 } && Key.All(c => char.IsAsciiLetterOrDigit(c) || c == '_') &&
        Label is { Length: >= 1 and <= 80 } && !string.IsNullOrWhiteSpace(Label) &&
        Type is "text" or "textarea" or "number" or "decimal" or "currency" or "date" or "dateRange" or "email" or "phone" or
            "select" or "radio" or "multiSelect" or "checkbox" or "toggle" or "heading" or "description" or "user" or "department" or "file" &&
        (WhenKey is null) == (WhenValue is null) && WhenValue?.Length is not > 100 &&
        (RequiredWhenKey is null) == (RequiredWhenValue is null) && RequiredWhenValue?.Length is not > 100 &&
        Step is >= 1 and <= 5 &&
        Help?.Length is not > 180 && MaxLength is null or (>= 1 and <= 2000) &&
        Placeholder?.Length is not > 120 && MinLength is null or (>= 0 and <= 2000) &&
        (MinLength is null || MinLength <= (MaxLength ?? 2000)) &&
        (Minimum is null && Maximum is null || Type is "number" or "decimal" or "currency" &&
            (Minimum is null || Maximum is null || Minimum <= Maximum)) &&
        (Pattern is null || Pattern.Length is > 0 and <= 128 && MatchesPattern(Pattern, "" ) is not null) &&
        (!Static || !Required && DefaultValue is null && RequiredWhenKey is null) &&
        (Type == "file" ? (FileLimitKiB is null or (>= 1 and <= 10240)) && DefaultValue is null &&
            (FileExtensions is null || FileExtensions.Length is >= 1 and <= 12 &&
                FileExtensions.All(extension => extension is { Length: >= 1 and <= 12 } && extension.All(char.IsAsciiLetterOrDigit)))
            : FileLimitKiB is null && FileExtensions is null) &&
        (Type is "select" or "radio" or "multiSelect"
            ? Options is { Length: >= 1 and <= 20 } && Options.All(x => !string.IsNullOrWhiteSpace(x) && x.Length <= 100) &&
                Options.Distinct(StringComparer.Ordinal).Count() == Options.Length
            : Options is null) &&
        (DefaultValue is null || Type is not ("user" or "department") && Accepts(DefaultValue));

    [System.Text.Json.Serialization.JsonIgnore]
    public bool Static => Type is "heading" or "description";

    public bool Visible(IReadOnlyDictionary<string, string?> accepted) =>
        WhenKey is null || accepted.TryGetValue(WhenKey, out var value) && value == WhenValue;

    public bool Accepts(string value, bool conditionalRequired = false) => !Static && value.Length <= (MaxLength ?? 2000) &&
        (value.Length == 0 ? !Required && !conditionalRequired : value.Length >= (MinLength ?? 0) &&
        (Pattern is null || MatchesPattern(Pattern, value) == true) && Type switch
    {
        "number" or "decimal" or "currency" => AcceptsNumber(value),
        "date" => DateOnly.TryParseExact(value, "yyyy-MM-dd", out _),
        "dateRange" => value.Split('/') is [var from, var to] &&
            DateOnly.TryParseExact(from, "yyyy-MM-dd", out var start) && DateOnly.TryParseExact(to, "yyyy-MM-dd", out var end) && start <= end,
        "email" => System.Net.Mail.MailAddress.TryCreate(value, out var email) && email.Address == value,
        "phone" => value.Count(char.IsAsciiDigit) is >= 7 and <= 15 &&
            value.All(c => char.IsAsciiDigit(c) || c is '+' or ' ' or '-' or '(' or ')'),
        "select" or "radio" => Options!.Contains(value, StringComparer.Ordinal),
        "multiSelect" => AcceptsMultiple(value),
        "checkbox" or "toggle" => value is "true" or "false",
        "department" => Guid.TryParseExact(value, "D", out _),
        "user" => value.Length <= 255,
        _ => true
    });

    private bool AcceptsNumber(string value) =>
        decimal.TryParse(value, NumberStyles.AllowLeadingSign | (Type == "number" ? NumberStyles.None : NumberStyles.AllowDecimalPoint),
            CultureInfo.InvariantCulture, out var number) &&
        (Type != "currency" || number == decimal.Round(number, 2)) &&
        (Minimum is null || number >= Minimum) && (Maximum is null || number <= Maximum);

    private bool AcceptsMultiple(string value)
    {
        try
        {
            var selections = JsonSerializer.Deserialize<string[]>(value);
            return selections is { Length: > 0 and <= 20 } && selections.Distinct(StringComparer.Ordinal).Count() == selections.Length &&
                selections.All(option => Options?.Contains(option, StringComparer.Ordinal) == true);
        }
        catch (JsonException) { return false; }
    }

    private static bool? MatchesPattern(string pattern, string value)
    {
        try { return Regex.IsMatch(value, $"\\A(?:{pattern})\\z", RegexOptions.NonBacktracking | RegexOptions.CultureInvariant, TimeSpan.FromMilliseconds(25)); }
        catch (Exception error) when (error is ArgumentException or NotSupportedException or RegexMatchTimeoutException) { return null; }
    }
}

internal sealed record StudioFormSchema(string Name, StudioField?[] Fields)
{
    public async Task<bool> ValidSelectionsAsync(FlowOpsDb db, Guid org, Dictionary<string, string?> answers, CancellationToken token)
    {
        foreach (var field in Fields.OfType<StudioField>())
        {
            if (answers.GetValueOrDefault(field.Key) is not { Length: > 0 } value) continue;
            if (field.Type == "user" && !await db.Memberships.AnyAsync(row => row.OrganizationId == org && row.Subject == value, token))
                return false;
            if (field.Type == "department" && (!Guid.TryParse(value, out var id) ||
                !await db.OrganizationGroups.AnyAsync(row => row.OrganizationId == org && row.Id == id && row.Kind == "department", token)))
                return false;
        }
        return true;
    }

    public bool Valid()
    {
        if (string.IsNullOrWhiteSpace(Name) || Name.Length is < 3 or > 100 || Fields is not { Length: <= 20 }) return false;
        var seen = new HashSet<string>(StringComparer.Ordinal);
        var inputs = new HashSet<string>(StringComparer.Ordinal);
        var files = 0;
        var step = 1;
        foreach (var field in Fields)
        {
            if (field is null || !field.Valid() || field.WhenKey is not null && !inputs.Contains(field.WhenKey) ||
                field.RequiredWhenKey is not null && !inputs.Contains(field.RequiredWhenKey) ||
                !seen.Add(field.Key) || field.Step < step || field.Step > step + 1 || seen.Count == 1 && field.Step != 1) return false;
            step = field.Step;
            if (field.Type == "file" && ++files > 1) return false;
            if (!field.Static && field.Type != "file") inputs.Add(field.Key);
        }
        return files == 0 || Fields.Single(field => field?.Type == "file")!.Step == step;
    }

    public Dictionary<string, string?>? Validate(IReadOnlyDictionary<string, string?> answers, bool draft = false)
    {
        if (answers.Keys.Except(Fields.Select(field => field!.Key), StringComparer.Ordinal).Any()) return null;
        var accepted = new Dictionary<string, string?>(StringComparer.Ordinal);
        foreach (var field in Fields.Select(field => field!))
        {
            var supplied = answers.GetValueOrDefault(field.Key)?.Trim();
            if (field.Static || field.Type == "file")
            {
                if (supplied is { Length: > 0 }) return null;
                continue;
            }
            var value = supplied ?? field.DefaultValue ?? "";
            if (!field.Visible(accepted))
            {
                if (value.Length > 0) return null;
                continue;
            }
            var conditionalRequired = field.RequiredWhenKey is not null &&
                accepted.TryGetValue(field.RequiredWhenKey, out var dependency) && dependency == field.RequiredWhenValue;
            if ((!draft || value.Length > 0) && !field.Accepts(value, conditionalRequired)) return null;
            accepted.Add(field.Key, value);
        }
        return accepted;
    }
}

internal sealed record StudioFormBinding(Guid Id, int Version, Dictionary<string, string>? InputMap = null)
{
    public bool ValidFor(StudioFormSchema schema)
    {
        if (InputMap is null) return true;
        var keys = schema.Fields.Where(field => !field!.Static && field.Type != "file").Select(field => field!.Key).ToHashSet(StringComparer.Ordinal);
        return InputMap.Count <= keys.Count && InputMap.Keys.All(keys.Contains) &&
            InputMap.Values.All(value => value is { Length: >= 1 and <= 64 } &&
                value.All(c => char.IsAsciiLetterOrDigit(c) || c == '_')) &&
            keys.Select(key => InputMap.GetValueOrDefault(key, key)).Distinct(StringComparer.OrdinalIgnoreCase).Count() == keys.Count;
    }

    public string InputName(string key) => InputMap?.GetValueOrDefault(key) ?? key;
}

internal sealed class StudioSubmissionDraft
{
    public Guid OrganizationId { get; init; }
    public Guid ProcessId { get; init; }
    public required string Subject { get; init; }
    public int ProcessVersion { get; init; }
    public int Revision { get; set; }
    public required string Answers { get; set; }
}

internal sealed class StudioFormDefinition
{
    public Guid OrganizationId { get; init; }
    public Guid Id { get; init; } = Guid.NewGuid();
    public required string Name { get; set; }
    public required string Draft { get; set; }
    public int Revision { get; set; } = 1;
    public int Version { get; set; }
    public int PublishedRevision { get; set; }
}

internal sealed class StudioFormVersion
{
    public Guid OrganizationId { get; init; }
    public Guid FormId { get; init; }
    public int Number { get; init; }
    public required string Schema { get; init; }
    public DateTimeOffset PublishedAt { get; init; } = DateTimeOffset.UtcNow;

    public static async Task<StudioFormSchema?> ResolveAsync(FlowOpsDb db, Guid org, StudioFormBinding binding, CancellationToken token)
    {
        var schema = await db.StudioFormVersions.Where(row => row.OrganizationId == org &&
            row.FormId == binding.Id && row.Number == binding.Version).Select(row => row.Schema).SingleOrDefaultAsync(token);
        return schema is null ? null : JsonSerializer.Deserialize<StudioFormSchema>(schema, JsonSerializerOptions.Web);
    }
}
