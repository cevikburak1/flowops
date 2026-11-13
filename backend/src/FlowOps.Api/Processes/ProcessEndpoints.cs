using System.Security.Claims;
using System.Security.Cryptography;
using System.Text.Json;
using Elsa.Workflows.Management;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace FlowOps.Api;

internal static class ProcessEndpoints
{
    public const string Editor = "ProcessEditor";
    public const string Operator = "ProcessOperator";

    public static void Map(RouteGroupBuilder tenant)
    {
        tenant.MapGet("/process-tasks", MyTasksAsync);
        var routes = tenant.MapGroup("/processes");
        routes.MapGet("", ListAsync);
        routes.MapPost("", CreateAsync);
        routes.MapGet("/{processId:guid}", GetAsync);
        routes.MapPost("/{processId:guid}/draft", SaveAsync);
        routes.MapPost("/{processId:guid}/publish", PublishAsync);
        routes.MapPost("/{processId:guid}/run", RunAsync).WithMetadata(new RequestSizeLimitAttribute(ProcessObjectStore.MaxRequestLength));
        routes.MapPost("/{processId:guid}/webhook", WebhookAsync).WithMetadata(new RequestSizeLimitAttribute(ProcessObjectStore.MaxRequestLength));
        routes.MapGet("/{processId:guid}/runs/{runId:guid}/attachment", DownloadAsync);
        routes.MapPost("/{processId:guid}/runs/{runId:guid}/tasks/{stepId}/complete", CompleteTaskAsync);
    }

    private static async Task<IResult> MyTasksAsync(Guid organizationId, int? page, ClaimsPrincipal user, FlowOpsDb db, CancellationToken token)
    {
        if (await AccessAsync(db, organizationId, user, token) is null) return Results.NotFound();
        var subject = PurchaseEndpoints.Subject(user);
        var size = page is null ? 50 : 10;
        var current = Math.Clamp(page ?? 1, 1, 10000);
        var items = await (from task in db.ProcessTasks
            join run in db.ProcessRuns on new { task.OrganizationId, Id = task.RunId } equals new { run.OrganizationId, run.Id }
            where task.OrganizationId == organizationId && task.Assignee == subject && task.CompletedAt == null && run.Status == "running"
            orderby task.AssignedAt, task.StepId, task.RunId
            select new { run.ProcessId, task.RunId, task.StepId, task.Title })
            .Skip((current - 1) * size).Take(size + 1).ToListAsync(token);
        return page is null ? Results.Ok(items.Take(size)) : Results.Ok(new { Items = items.Take(size), Page = current, HasMore = items.Count > size });
    }

    private static Task<ProcessAccess?> AccessAsync(FlowOpsDb db, Guid org, ClaimsPrincipal user, CancellationToken token) =>
        ProcessPermissions.FindAsync(db, org, PurchaseEndpoints.Subject(user), token);

    private static async Task<bool> ValidAsync(ProcessSpec spec, ProcessTargets targets, FlowOpsDb db, Guid org, CancellationToken token)
    {
        if (!spec.Valid(targets)) return false;
        var assignees = ProcessSpec.Assignees(spec.Steps!).Distinct().ToArray();
        return assignees.Length == 0 || assignees.Length == await db.Memberships.CountAsync(
            x => x.OrganizationId == org && assignees.Contains(x.Subject), token);
    }

    private static async Task<object[]> MembersAsync(FlowOpsDb db, Guid org, bool edit, CancellationToken token) =>
        edit ? (await db.Memberships.Where(x => x.OrganizationId == org).OrderBy(x => x.Email).ThenBy(x => x.Subject)
            .Select(x => new { x.Subject, x.Email }).ToArrayAsync(token)) : [];

    private static IResult Invalid(string code) => Results.BadRequest(new { code });
    private static IResult Conflict(string code) => Results.Conflict(new { code });

    private static async Task<IResult> ListAsync(Guid organizationId, int? page, ClaimsPrincipal user, FlowOpsDb db, ProcessTargets targets, CancellationToken token)
    {
        var access = await AccessAsync(db, organizationId, user, token);
        if (access is null) return Results.NotFound();
        var edit = access.Grant.Edit;
        const int size = 10;
        var current = Math.Clamp(page ?? 1, 1, 10000);
        var items = await db.Database.SqlQuery<ProcessListItem>($"""
            SELECT "Id", "Version", (CASE WHEN {edit} THEN "Draft" ELSE "Published" END)::jsonb ->> 'name' AS "Name"
            FROM flowops."Processes"
            WHERE "OrganizationId" = {organizationId} AND ({edit} OR "Version" > 0)
            ORDER BY "Name", "Id"
            LIMIT {size + 1} OFFSET {(current - 1) * size}
            """).ToListAsync(token);
        return Results.Ok(new { CanEdit = edit, Targets = edit ? targets.Names.ToArray() : [],
            Members = await MembersAsync(db, organizationId, edit, token),
            Items = items.Take(size), Page = current, HasMore = items.Count > size });
    }

    private static async Task<IResult> GetAsync(Guid organizationId, Guid processId, Guid? runId, int? page, ClaimsPrincipal user, FlowOpsDb db,
        ProcessTargets targets, ProcessObjectStore storage, CancellationToken token)
    {
        var subject = PurchaseEndpoints.Subject(user);
        var access = await AccessAsync(db, organizationId, user, token);
        if (access is null) return Results.NotFound();
        var process = await db.Processes.FindAsync([organizationId, processId], token);
        var edit = access.Grant.Edit;
        if (process is null || (!edit && process.Published is null)) return Results.NotFound();
        var inspect = access.Grant.Inspect;
        var assignedRuns = db.ProcessTasks.Where(t => t.OrganizationId == organizationId && t.Assignee == subject).Select(t => t.RunId);
        const int size = 8;
        var current = Math.Clamp(page ?? 1, 1, 10000);
        var runs = await db.ProcessRuns.AsNoTracking().Where(x => x.OrganizationId == organizationId &&
            x.ProcessId == processId && (inspect || x.Subject == subject || assignedRuns.Contains(x.Id)))
            .OrderByDescending(x => x.Id == runId)
            .ThenByDescending(x => db.ProcessTasks.Any(t => t.OrganizationId == organizationId && t.RunId == x.Id &&
                t.Assignee == subject && t.CompletedAt == null && x.Status == "running"))
            .ThenByDescending(x => x.CreatedAt).ThenBy(x => x.Id)
            .Skip((current - 1) * size).Take(size + 1)
            .Select(x => new { x.Id, x.Status, x.Version, x.CreatedAt, x.Answers,
                Attachment = db.ProcessAttachments.Where(a => a.OrganizationId == organizationId && a.RunId == x.Id)
                    .Select(a => a.Name).SingleOrDefault() }).ToListAsync(token);
        var hasMore = runs.Count > size;
        if (hasMore) runs.RemoveAt(size);
        var ids = runs.Select(x => x.Id).ToArray();
        var ownRuns = await db.ProcessRuns.Where(r => r.OrganizationId == organizationId && ids.Contains(r.Id) && r.Subject == subject)
            .Select(r => r.Id).ToListAsync(token);
        var tasks = await db.ProcessTasks.Where(t => t.OrganizationId == organizationId && ids.Contains(t.RunId) &&
                (inspect || t.Assignee == subject || ownRuns.Contains(t.RunId)))
            .OrderBy(t => t.AssignedAt).ThenBy(t => t.StepId)
            .Select(t => new { t.RunId, t.StepId, t.Title, t.AssignedAt, t.CompletedAt, CanComplete = t.Assignee == subject,
                Assignee = db.Memberships.Where(m => m.OrganizationId == organizationId && m.Subject == t.Assignee)
                    .Select(m => m.Email).SingleOrDefault() ?? t.Assignee })
            .ToListAsync(token);
        return Results.Ok(new { process.Id, process.Revision, process.Version, CanEdit = edit, CanRun = access.Grant.Run,
            AttachmentLimit = storage.Enabled ? ProcessObjectStore.FileLimit : ProcessObjectStore.InlineLimit,
            Draft = edit ? ProcessSpec.Read(process.Draft) : null,
            Published = process.Published is null ? null : ProcessSpec.Read(process.Published),
            Targets = edit ? targets.Names.ToArray() : [], Members = await MembersAsync(db, organizationId, edit, token),
            Runs = runs, Tasks = tasks, Page = current, HasMore = hasMore });
    }

    private static async Task<IResult> CreateAsync(Guid organizationId, ProcessSpec spec, ClaimsPrincipal user, FlowOpsDb db,
        ProcessTargets targets, CancellationToken token)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(token);
        if (await OrganizationEndpoints.LockAsync(db, organizationId, token) is null ||
            (await AccessAsync(db, organizationId, user, token))?.Grant.Edit != true) return Results.NotFound();
        if (!await ValidAsync(spec, targets, db, organizationId, token)) return Invalid("invalidProcess");
        var process = new ProcessDefinition { OrganizationId = organizationId, Draft = spec.Serialize() };
        db.Processes.Add(process);
        await db.SaveChangesAsync(token);
        await transaction.CommitAsync(token);
        return Results.Json(new { process.Id }, statusCode: 201);
    }

    private static async Task<IResult> SaveAsync(Guid organizationId, Guid processId, ProcessDraft input, ClaimsPrincipal user,
        FlowOpsDb db, ProcessTargets targets, CancellationToken token)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(token);
        if (await OrganizationEndpoints.LockAsync(db, organizationId, token) is null ||
            (await AccessAsync(db, organizationId, user, token))?.Grant.Edit != true) return Results.NotFound();
        var process = await db.Processes.FindAsync([organizationId, processId], token);
        if (process is null) return Results.NotFound();
        if (input.Revision != process.Revision) return Conflict("processChanged");
        if (input.Spec is null || !await ValidAsync(input.Spec, targets, db, organizationId, token)) return Invalid("invalidProcess");
        var draft = input.Spec.Serialize();
        if (draft == process.Draft) return Results.NoContent();
        process.Draft = draft;
        process.Revision++;
        await db.SaveChangesAsync(token);
        await transaction.CommitAsync(token);
        return Results.NoContent();
    }

    private static async Task<IResult> PublishAsync(Guid organizationId, Guid processId, ProcessPublication input,
        ClaimsPrincipal user, FlowOpsDb db, ProcessTargets targets, IWorkflowDefinitionPublisher publisher,
        IWorkflowDefinitionStore store, CancellationToken token)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(token);
        if (await OrganizationEndpoints.LockAsync(db, organizationId, token) is null ||
            (await AccessAsync(db, organizationId, user, token))?.Grant.Edit != true) return Results.NotFound();
        var process = await db.Processes.FindAsync([organizationId, processId], token);
        if (process is null) return Results.NotFound();
        if (input.Revision != process.Revision) return Conflict("processChanged");
        if (process.Draft == process.Published) return Results.NoContent();
        var spec = ProcessSpec.Read(process.Draft);
        if (!await ValidAsync(spec, targets, db, organizationId, token)) return Invalid("invalidProcess");
        process.ElsaDefinitionId = await ProcessCompiler.PublishAsync(process, spec, publisher, store, token);
        process.Published = process.Draft;
        process.Version++;
        await db.SaveChangesAsync(token);
        await transaction.CommitAsync(token);
        return Results.NoContent();
    }

    private static Task<IResult> RunAsync(Guid organizationId, Guid processId, ProcessSubmission input, ClaimsPrincipal user,
        FlowOpsDb db, ProcessObjectStore storage, CancellationToken token) => SubmitAsync(organizationId, processId, input, user, db, storage, "manual", token);

    private static Task<IResult> WebhookAsync(Guid organizationId, Guid processId, ProcessSubmission input, ClaimsPrincipal user,
        FlowOpsDb db, ProcessObjectStore storage, CancellationToken token) => SubmitAsync(organizationId, processId, input, user, db, storage, "webhook", token);

    private static async Task<IResult> SubmitAsync(Guid organizationId, Guid processId, ProcessSubmission input,
        ClaimsPrincipal user, FlowOpsDb db, ProcessObjectStore storage, string trigger, CancellationToken token)
    {
        var subject = PurchaseEndpoints.Subject(user);
        await using var transaction = await db.Database.BeginTransactionAsync(token);
        if (await OrganizationEndpoints.LockAsync(db, organizationId, token) is null ||
            (await AccessAsync(db, organizationId, user, token))?.Grant.Run != true) return Results.NotFound();
        var process = await db.Processes.FindAsync([organizationId, processId], token);
        if (process?.Published is null || process.ElsaDefinitionId is null) return Results.NotFound();
        if (input.Id == Guid.Empty || input.Answers is null) return Invalid("invalidAnswers");
        var fingerprint = Convert.ToHexString(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(new
        {
            Trigger = trigger, input.Version, Answers = new SortedDictionary<string, string?>(input.Answers, StringComparer.Ordinal), input.Attachment
        }, JsonSerializerOptions.Web)));
        if (await db.ProcessRuns.FindAsync([organizationId, input.Id], token) is { } existing)
            return existing.ProcessId == processId && existing.Subject == subject && existing.Fingerprint == fingerprint
                ? Results.Accepted(value: new { existing.Id }) : Conflict("processChanged");
        var spec = ProcessSpec.Read(process.Published);
        if (spec.Trigger != trigger || input.Version != process.Version) return Conflict("processChanged");
        var assignees = ProcessSpec.Assignees(spec.Steps!).Distinct().ToArray();
        if (assignees.Length > 0 && assignees.Length != await db.Memberships.CountAsync(
                x => x.OrganizationId == organizationId && assignees.Contains(x.Subject), token))
            return Conflict("assigneeUnavailable");
        var answers = input.Answers;
        if (answers.Keys.Except(spec.Fields!.Select(x => x!.Key!), StringComparer.Ordinal).Any() ||
            spec.Fields!.Any(x => !x!.ValidAnswer(answers.GetValueOrDefault(x.Key!)?.Trim() ?? ""))) return Invalid("invalidAnswers");
        if ((input.Attachment is null && spec.Attachment == "required") ||
            (input.Attachment is not null && spec.Attachment == "none")) return Invalid("invalidAttachment");
        if (input.Attachment is { } file)
        {
            if (file.Name is not { Length: >= 1 and <= 120 } || string.IsNullOrWhiteSpace(file.Name) ||
                file.Name.Any(c => char.IsControl(c) || c is '/' or '\\') ||
                file.Content is not { Length: > 0 and <= ProcessObjectStore.MaxEncodedLength }) return Invalid("invalidAttachment");
            byte[] bytes;
            try { bytes = Convert.FromBase64String(file.Content); }
            catch (FormatException) { return Invalid("invalidAttachment"); }
            if (bytes.Length == 0 || bytes.Length > ProcessObjectStore.FileLimit) return Invalid("invalidAttachment");
            string? objectKey = null;
            if (bytes.Length > ProcessObjectStore.InlineLimit)
            {
                if (!storage.Enabled) return Conflict("storageUnavailable");
                // The organization lock and run ID check prevent replacing an attachment for a committed run.
                objectKey = $"processes/{organizationId:N}/{input.Id:N}";
                try { await storage.PutAsync(objectKey, bytes, token); }
                catch (Exception exception) when (!token.IsCancellationRequested &&
                    exception is Amazon.Runtime.AmazonServiceException or HttpRequestException or OperationCanceledException)
                {
                    return Results.Json(new { code = "storageUnavailable" }, statusCode: 503);
                }
            }
            db.ProcessAttachments.Add(new ProcessAttachment { OrganizationId = organizationId, RunId = input.Id,
                Name = file.Name, Content = objectKey is null ? bytes : null, ObjectKey = objectKey });
        }
        db.ProcessRuns.Add(new ProcessRun { OrganizationId = organizationId, Id = input.Id, ProcessId = processId,
            Version = process.Version, ElsaDefinitionId = process.ElsaDefinitionId, Subject = subject,
            Answers = JsonSerializer.Serialize(spec.Fields!.Select(field =>
                new { field!.Label, Value = answers.GetValueOrDefault(field.Key!)?.Trim() ?? "" }), JsonSerializerOptions.Web),
            Fingerprint = fingerprint });
        await db.SaveChangesAsync(token);
        await transaction.CommitAsync(token);
        return Results.Accepted(value: new { input.Id });
    }

    private static async Task<IResult> DownloadAsync(Guid organizationId, Guid processId, Guid runId, ClaimsPrincipal user,
        FlowOpsDb db, ProcessObjectStore storage, HttpContext context, CancellationToken token)
    {
        var subject = PurchaseEndpoints.Subject(user);
        var access = await AccessAsync(db, organizationId, user, token);
        if (access is null) return Results.NotFound();
        var inspect = access.Grant.Inspect;
        var attachment = await (from file in db.ProcessAttachments
            join run in db.ProcessRuns on new { file.OrganizationId, Id = file.RunId } equals new { run.OrganizationId, run.Id }
            where file.OrganizationId == organizationId && file.RunId == runId && run.ProcessId == processId &&
                (inspect || run.Subject == subject || db.ProcessTasks.Any(t =>
                    t.OrganizationId == organizationId && t.RunId == runId && t.Assignee == subject))
            select file).SingleOrDefaultAsync(token);
        if (attachment is null) return Results.NotFound();
        if (attachment.Content is not null) return Results.File(attachment.Content, "application/octet-stream", attachment.Name);
        if (!storage.Enabled || attachment.ObjectKey is null)
            return Results.Json(new { code = "storageUnavailable" }, statusCode: 503);
        try
        {
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(token);
            timeout.CancelAfter(TimeSpan.FromSeconds(20));
            var response = await storage.GetAsync(attachment.ObjectKey, timeout.Token);
            context.Response.RegisterForDispose(response);
            return Results.Stream(response.ResponseStream, "application/octet-stream", attachment.Name);
        }
        catch (Exception exception) when (!token.IsCancellationRequested &&
            exception is Amazon.Runtime.AmazonServiceException or HttpRequestException or OperationCanceledException)
        {
            return Results.Json(new { code = "storageUnavailable" }, statusCode: 503);
        }
    }

    private static async Task<IResult> CompleteTaskAsync(Guid organizationId, Guid processId, Guid runId, string stepId,
        ClaimsPrincipal user, FlowOpsDb db, CancellationToken token)
    {
        var subject = PurchaseEndpoints.Subject(user);
        await using var transaction = await db.Database.BeginTransactionAsync(token);
        if (await OrganizationEndpoints.LockAsync(db, organizationId, token) is null ||
            await AccessAsync(db, organizationId, user, token) is null) return Results.NotFound();
        var run = await db.ProcessRuns.FindAsync([organizationId, runId], token);
        var task = await db.ProcessTasks.FindAsync([organizationId, runId, stepId], token);
        if (run?.ProcessId != processId || task?.Assignee != subject) return Results.NotFound();
        if (task.CompletedAt is not null) return Results.NoContent();
        if (run.Status != "running") return Conflict("processChanged");
        run.NextAt = DateTimeOffset.UtcNow;
        task.CompletedAt = run.NextAt;
        await db.SaveChangesAsync(token);
        await transaction.CommitAsync(token);
        return Results.NoContent();
    }

    private sealed record ProcessDraft(int Revision, ProcessSpec? Spec);
    private sealed record ProcessListItem(Guid Id, int Version, string Name);
    private sealed record ProcessPublication(int Revision);
    private sealed record ProcessSubmission(Guid Id, int Version, Dictionary<string, string?>? Answers, FileInput? Attachment);
    private sealed record FileInput(string? Name, string? Content);
}
