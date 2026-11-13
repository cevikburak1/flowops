using System.Globalization;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Elsa.Extensions;
using Elsa.Workflows;
using Elsa.Workflows.Activities;
using Elsa.Workflows.Management;
using Elsa.Workflows.Models;
using Elsa.Workflows.Runtime;
using Microsoft.EntityFrameworkCore;

namespace FlowOps.Api;

internal sealed class ProcessTargets(IConfiguration configuration, IWebHostEnvironment environment)
{
    public IEnumerable<string> Names => configuration.GetSection("Processes:Targets").GetChildren()
        .Select(x => x.Key).Where(x => Resolve(x) is not null).Order();

    public Uri? Resolve(string? name)
    {
        if (name is not { Length: >= 1 and <= 64 } || name.Any(x => !char.IsAsciiLetterOrDigit(x) && x is not '-' and not '_'))
            return null;
        return Uri.TryCreate(configuration[$"Processes:Targets:{name}"], UriKind.Absolute, out var uri) &&
            (uri.Scheme == "https" || environment.IsDevelopment() && uri.Scheme == "http") &&
            string.IsNullOrEmpty(uri.UserInfo) && string.IsNullOrEmpty(uri.Fragment) ? uri : null;
    }
}

internal static class ProcessCompiler
{
    public static async Task<string> PublishAsync(ProcessDefinition process, ProcessSpec spec,
        IWorkflowDefinitionPublisher publisher, IWorkflowDefinitionStore store, CancellationToken token)
    {
        var digest = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(process.Draft)));
        var id = $"process-{process.OrganizationId:N}-{process.Id:N}-{process.Version + 1}-{digest}";
        if ((await store.FindAsync(new() { Id = id }, token))?.IsPublished == true) return id;
        var definition = await publisher.NewAsync(SequenceFor(spec.Steps!, "root"), token);
        definition.Id = id;
        definition.DefinitionId = id;
        definition.Name = spec.Name!.Trim();
        if (!(await publisher.PublishAsync(definition, token)).Succeeded)
            throw new InvalidOperationException("Elsa rejected the process definition.");
        return id;
    }

    private static Sequence SequenceFor(ProcessStep?[] steps, string prefix) => new()
    {
        Id = prefix,
        Activities = steps.Select((step, index) => ActivityFor(step!, $"{prefix}-{index}")).ToList()
    };

    private static IActivity ActivityFor(ProcessStep step, string id) => step.Kind switch
    {
        "timer" => new ProcessTimer { Id = id, Seconds = new(step.Seconds!.Value) },
        "http" => new ProcessHttp { Id = id, Target = new(step.Target!) },
        "assignment" => new ProcessAssignment { Id = id, Assignee = new(step.Assignee!), Title = new(step.Title!.Trim()) },
        "parallel" => new Fork
        {
            Id = id, JoinMode = ForkJoinMode.WaitAll,
            Branches = step.Branches!.Select((branch, index) => (IActivity)SequenceFor(branch!, $"{id}-branch-{index}")).ToList()
        },
        _ => throw new InvalidOperationException("Unsupported process step.")
    };
}

public sealed class ProcessAssignment : Activity
{
    public Input<string> Assignee { get; set; } = new("");
    public Input<string> Title { get; set; } = new("");

    protected override ValueTask ExecuteAsync(ActivityExecutionContext context)
    {
        context.CreateBookmark(ResumeAsync, new Dictionary<string, string>
        {
            ["flowopsTask"] = Id, ["flowopsAssignee"] = Assignee.Get(context), ["flowopsTaskTitle"] = Title.Get(context)
        });
        return ValueTask.CompletedTask;
    }

    private static ValueTask ResumeAsync(ActivityExecutionContext context) => context.CompleteActivityAsync();
}

public sealed class ProcessTimer : Activity
{
    public Input<int> Seconds { get; set; } = new(1);

    protected override ValueTask ExecuteAsync(ActivityExecutionContext context)
    {
        var dueAt = DateTimeOffset.UtcNow.AddSeconds(Seconds.Get(context));
        context.CreateBookmark(ResumeAsync, new Dictionary<string, string>
        {
            ["flowopsTimerDue"] = dueAt.ToString("O", CultureInfo.InvariantCulture)
        });
        return ValueTask.CompletedTask;
    }

    private static ValueTask ResumeAsync(ActivityExecutionContext context) => context.CompleteActivityAsync();
}

public sealed class ProcessHttp : Activity
{
    public Input<string> Target { get; set; } = new("");

    protected override async ValueTask ExecuteAsync(ActivityExecutionContext context)
    {
        var targets = context.GetRequiredService<ProcessTargets>();
        var uri = targets.Resolve(Target.Get(context)) ?? throw new InvalidOperationException("Process HTTP target is unavailable.");
        var instanceId = context.WorkflowExecutionContext.Id;
        var (organizationId, runId) = ProcessWorker.ParseInstance(instanceId);
        using var request = new HttpRequestMessage(HttpMethod.Post, uri);
        request.Headers.Add("Idempotency-Key", $"{runId:N}-{Id}");
        request.Content = JsonContent.Create(new { OrganizationId = organizationId, RunId = runId, StepId = Id });
        var factory = context.GetRequiredService<IHttpClientFactory>();
        using var response = await factory.CreateClient("flowops-process").SendAsync(request,
            HttpCompletionOption.ResponseHeadersRead, context.CancellationToken);
        if (!response.IsSuccessStatusCode) throw new InvalidOperationException($"Process HTTP target returned {(int)response.StatusCode}.");
        await context.CompleteActivityAsync();
    }
}

internal sealed class ProcessWorker(IServiceScopeFactory scopes, ILogger<ProcessWorker> logger) : BackgroundService
{
    internal static string InstanceId(Guid organizationId, Guid runId) => $"process:{organizationId:N}:{runId:N}";

    internal static (Guid OrganizationId, Guid RunId) ParseInstance(string id) =>
        (Guid.ParseExact(id.AsSpan(8, 32), "N"), Guid.ParseExact(id.AsSpan(41, 32), "N"));

    protected override async Task ExecuteAsync(CancellationToken token)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(1));
        while (await timer.WaitForNextTickAsync(token))
        {
            try
            {
                for (var count = 0; count < 10 && await AdvanceAsync(token); count++) { }
            }
            catch (Exception exception) when (!token.IsCancellationRequested)
            {
                logger.LogWarning("Process worker iteration failed ({ErrorType})", exception.GetType().Name);
            }
        }
    }

    private async Task<bool> AdvanceAsync(CancellationToken token)
    {
        await using var scope = scopes.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<FlowOpsDb>();
        await using var transaction = await db.Database.BeginTransactionAsync(token);
        var now = DateTimeOffset.UtcNow;
        var ready = await db.ProcessRuns.AsNoTracking().Where(x => x.Status == "running" && x.NextAt <= now)
            .OrderBy(x => x.NextAt).ThenBy(x => x.Id).Select(x => new { x.OrganizationId, x.Id }).FirstOrDefaultAsync(token);
        if (ready is null || await OrganizationEndpoints.LockAsync(db, ready.OrganizationId, token) is null) return false;
        var candidates = await db.ProcessRuns.FromSqlInterpolated($"""
            SELECT * FROM flowops."ProcessRuns"
            WHERE "OrganizationId" = {ready.OrganizationId} AND "Id" = {ready.Id} AND "Status" = 'running' AND "NextAt" <= {now}
            ORDER BY "NextAt", "Id" LIMIT 1 FOR UPDATE SKIP LOCKED
            """).ToListAsync(token);
        if (candidates.SingleOrDefault() is not { } run) return false;
        try
        {
            var runtime = scope.ServiceProvider.GetRequiredService<IWorkflowRuntime>();
            var instance = InstanceId(run.OrganizationId, run.Id);
            var client = await runtime.CreateClientAsync(instance, token);
            if (!await client.InstanceExistsAsync(token))
                await client.CreateAndRunInstanceAsync(new()
                {
                    WorkflowDefinitionHandle = WorkflowDefinitionHandle.ByDefinitionVersionId(run.ElsaDefinitionId),
                    CorrelationId = instance,
                    Input = StudioInputs(run.Answers)
                }, token);

            var state = await client.ExportStateAsync(token);
            var tasks = await db.ProcessTasks.Where(x => x.OrganizationId == run.OrganizationId && x.RunId == run.Id)
                .ToDictionaryAsync(x => x.StepId, token);
            foreach (var bookmark in state.Bookmarks.Where(x => DueAt(x.Metadata) <= now ||
                (TaskId(x.Metadata) is string step && tasks.GetValueOrDefault(step)?.CompletedAt is not null)).ToArray())
            {
                if (state.Bookmarks.Any(x => x.Id == bookmark.Id))
                    await client.RunInstanceAsync(new() { BookmarkId = bookmark.Id }, token);
                state = await client.ExportStateAsync(token);
            }
            run.Attempts = 0;
            run.Status = state.SubStatus switch
            {
                WorkflowSubStatus.Finished => "completed",
                WorkflowSubStatus.Suspended when state.Bookmarks.Count > 0 => "running",
                _ => "failed"
            };
            if (run.Status == "running")
            {
                foreach (var bookmark in state.Bookmarks)
                {
                    if (TaskId(bookmark.Metadata) is not string step || tasks.ContainsKey(step)) continue;
                    var assignee = bookmark.Metadata!["flowopsAssignee"];
                    if (!await db.Memberships.AnyAsync(x => x.OrganizationId == run.OrganizationId && x.Subject == assignee, token))
                    {
                        run.Status = "failed";
                        break;
                    }
                    var (dueAt, warnAt) = ApprovalSla.ProcessTaskSchedule(run.SlaSnapshot, now, run.DueAt);
                    var task = new ProcessTask { OrganizationId = run.OrganizationId, RunId = run.Id, StepId = step,
                        Assignee = assignee, Title = bookmark.Metadata["flowopsTaskTitle"],
                        DueAt = dueAt, WarnAt = warnAt };
                    tasks.Add(step, task);
                    db.ProcessTasks.Add(task);
                }
                run.NextAt = state.Bookmarks.Min(x => DueAt(x.Metadata));
            }
        }
        catch (Exception exception) when (!token.IsCancellationRequested)
        {
            run.Attempts++;
            run.NextAt = now.AddSeconds(1 << run.Attempts);
            if (run.Attempts >= 5) run.Status = "failed";
            logger.LogWarning("Process {RunId} execution attempt {Attempt} failed ({ErrorType})", run.Id, run.Attempts, exception.GetType().Name);
        }
        await db.SaveChangesAsync(token);
        await transaction.CommitAsync(token);
        return true;
    }

    internal static Dictionary<string, object> StudioInputs(string answers)
    {
        using var document = JsonDocument.Parse(answers);
        return document.RootElement.EnumerateArray()
            .Where(answer => answer.TryGetProperty("key", out _))
            .ToDictionary(answer => (answer.TryGetProperty("inputName", out var mapped) ? mapped : answer.GetProperty("key")).GetString()!,
                answer => (object)answer.GetProperty("value").GetString()!, StringComparer.Ordinal);
    }

    private static string? TaskId(IDictionary<string, string>? metadata) =>
        metadata is not null && metadata.TryGetValue("flowopsTask", out var step) ? step : null;

    private static DateTimeOffset DueAt(IDictionary<string, string>? metadata) =>
        metadata is not null && metadata.TryGetValue("flowopsTimerDue", out var due) &&
        DateTimeOffset.TryParseExact(due, "O", CultureInfo.InvariantCulture, DateTimeStyles.None, out var result)
            ? result : DateTimeOffset.MaxValue;
}
