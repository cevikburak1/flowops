using Xunit;

namespace FlowOps.Api.Tests;

public sealed class SimulationTests
{
    [Fact]
    public void BusinessCalendarSkipsHolidayAndUsesPostTransitionOffset()
    {
        var calendar = new BusinessCalendar("America/New_York",
            [DayOfWeek.Monday, DayOfWeek.Tuesday, DayOfWeek.Wednesday, DayOfWeek.Thursday, DayOfWeek.Friday],
            new(9, 0), new(17, 0), [new(2026, 3, 9)]);
        var start = new DateTimeOffset(2026, 3, 6, 21, 0, 0, TimeSpan.Zero);
        Assert.Equal(new DateTimeOffset(2026, 3, 10, 14, 0, 0, TimeSpan.Zero), calendar.Deadline(start, 120));
    }

    [Fact]
    public void ProcessTaskDeadlineUsesEarlierWorkflowDeadline()
    {
        var calendar = new BusinessCalendar("UTC", [DayOfWeek.Monday], new(9, 0), new(17, 0), []);
        var policy = System.Text.Json.JsonSerializer.Serialize(new ApprovalSlaPolicy(calendar, 120, null, WorkflowMinutes: 90),
            System.Text.Json.JsonSerializerOptions.Web);
        var started = new DateTimeOffset(2026, 11, 16, 9, 0, 0, TimeSpan.Zero);
        var workflowDue = ApprovalSla.WorkflowDeadline(policy, started);
        Assert.Equal(started.AddMinutes(90), workflowDue);
        Assert.Equal(workflowDue, ApprovalSla.ProcessTaskDeadline(policy, started.AddMinutes(20), workflowDue));
    }

    [Theory]
    [InlineData(3, 8, 8)]
    [InlineData(11, 1, 7)]
    public void BusinessCalendarAccountsForTransitionInsideShift(int month, int day, int expectedUtcHour)
    {
        var calendar = new BusinessCalendar("America/New_York", [DayOfWeek.Sunday], new(1, 0), new(5, 0), []);
        var start = new DateTimeOffset(2026, month, day, 4, 0, 0, TimeSpan.Zero);
        Assert.Equal(new DateTimeOffset(2026, month, day, expectedUtcHour, 0, 0, TimeSpan.Zero), calendar.Deadline(start, 120));
    }
    [Fact]
    public void MatrixUsesExplicitCurrencyPriorityAndExclusiveUpperBound()
    {
        var rules = new ApprovalMatrixRule[]
        {
            new(2, "TRY", 0, null, null, null, null, "managerFinance"),
            new(1, "TRY", 0, 20_000, "equipment", null, null, "manager")
        };
        Assert.True(ApprovalMatrix.Valid(rules, "TRY"));
        Assert.False(ApprovalMatrix.Valid([rules[0], rules[1] with { Priority = 2 }], "TRY"));
        Assert.Equal("manager", ApprovalMatrix.Match(rules, "TRY", 19_999, "equipment", "Employee", null)?.Route);
        Assert.Equal("managerFinance", ApprovalMatrix.Match(rules, "TRY", 20_000, "equipment", "Employee", null)?.Route);
        Assert.Null(ApprovalMatrix.Match(rules, "USD", 100, "equipment", "Employee", null));
    }
    [Theory]
    [InlineData(2000, "approved", "rejected", "below", "approved")]
    [InlineData(2001, "approved", "rejected", "above", "rejected")]
    [InlineData(2001, "rejected", "approved", "below", "rejected")]
    public void FinanceBranchMatchesPurchaseSemantics(int amount, string manager, string finance, string branch, string outcome)
    {
        var result = WorkflowSimulation.Run(PurchaseGraph.Default, 2000,
            new(1, null, amount, manager, finance), TestContext.Current.CancellationToken);
        Assert.Equal(outcome, result.Outcome);
        Assert.Equal(branch, Assert.Single(result.Steps, step => step.Kind == "threshold").Branch);
        Assert.Equal(branch == "above", result.Steps.Any(step => step.Kind == "finance"));
    }

    [Fact]
    public void ReportsUnreachableStepsBeforeExecution()
    {
        var graph = PurchaseGraph.Default with { Edges = [new("start", "manager")] };
        var result = WorkflowSimulation.Run(graph, 2000, new(null, 1, 10, "approved", "approved"),
            TestContext.Current.CancellationToken);
        Assert.Equal("invalid", result.Outcome);
        Assert.Empty(result.Steps);
        Assert.Contains(result.Issues, issue => issue is { Code: "unreachable", NodeId: "finance" });
    }

    [Fact]
    public void FormRequiresOnlyVisibleFields()
    {
        var schema = new StudioFormSchema("Supplier", [
            new("kind", "Supplier type", "select", true, Options: ["local", "foreign"]),
            new("ref", "Reference", "text", true, "kind", "foreign")
        ]);
        Assert.True(schema.Valid());
        Assert.NotNull(schema.Validate(new Dictionary<string, string?> { ["kind"] = "local" }));
        Assert.Null(schema.Validate(new Dictionary<string, string?> { ["kind"] = "local", ["ref"] = "hidden" }));
        Assert.Null(schema.Validate(new Dictionary<string, string?> { ["kind"] = "foreign" }));
        Assert.Equal("REF-42", schema.Validate(new Dictionary<string, string?> { ["kind"] = "foreign", ["ref"] = "REF-42" })!["ref"]);
        var mapped = ProcessWorker.StudioInputs("""[{"key":"ref","label":"Reference","value":"REF-42","inputName":"supplier_ref"}]""");
        Assert.Equal("REF-42", mapped["supplier_ref"]);
        Assert.Empty(ProcessWorker.StudioInputs("""[{"label":"Legacy","value":"same"}]"""));
    }

    [Fact]
    public void StudioFormValidatesTypedAnswersAndSafePatterns()
    {
        var schema = new StudioFormSchema("Travel", [
            new("kind", "Type", "radio", true, Options: ["local", "foreign"]),
            new("cost", "Cost", "currency", false, Minimum: 1, Maximum: 100),
            new("dates", "Dates", "dateRange", false),
            new("tags", "Tags", "multiSelect", false, Options: ["air", "rail"]),
            new("code", "Code", "text", false, Pattern: "[A-Z]{2}[0-9]{2}",
                RequiredWhenKey: "kind", RequiredWhenValue: "foreign")
        ]);
        Assert.True(schema.Valid());
        var answers = new Dictionary<string, string?> { ["kind"] = "foreign", ["cost"] = "25.50",
            ["dates"] = "2026-11-14/2026-11-15", ["tags"] = """["air","rail"]""", ["code"] = "TR42" };
        Assert.NotNull(schema.Validate(answers));
        Assert.Null(schema.Validate(new Dictionary<string, string?>(answers) { ["cost"] = "25.555" }));
        Assert.Null(schema.Validate(new Dictionary<string, string?>(answers) { ["code"] = "" }));
        Assert.Null(schema.Validate(new Dictionary<string, string?>(answers) { ["tags"] = """["air","air"]""" }));
        Assert.False(new StudioField("x", "X", "text", false, Pattern: "(a)\\1").Valid());
    }
}
