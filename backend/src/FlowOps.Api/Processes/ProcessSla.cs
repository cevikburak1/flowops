using Microsoft.EntityFrameworkCore;

namespace FlowOps.Api;

internal static class ProcessSla
{
    internal static async Task EscalateAsync(IServiceScopeFactory scopes, CancellationToken token)
    {
        await using var scope = scopes.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<FlowOpsDb>();
        await using var transaction = await db.Database.BeginTransactionAsync(token);
        var now = DateTimeOffset.UtcNow;
        var organizations = await db.Organizations.FromSqlInterpolated($"""
            SELECT org.* FROM flowops."Organizations" org
            WHERE EXISTS (
                SELECT 1 FROM flowops."ProcessRuns" r
                WHERE r."OrganizationId" = org."Id" AND r."Status" = 'running' AND
                    ((r."EscalatedAt" IS NULL AND r."DueAt" <= {now}) OR EXISTS (
                        SELECT 1 FROM flowops."ProcessTasks" t
                        WHERE t."OrganizationId" = r."OrganizationId" AND t."RunId" = r."Id" AND t."CompletedAt" IS NULL AND
                            ((t."EscalatedAt" IS NULL AND t."DueAt" <= {now}) OR
                             (t."WarnedAt" IS NULL AND t."WarnAt" <= {now} AND t."DueAt" > {now}))))
            )
            ORDER BY org."Id" LIMIT 1 FOR UPDATE OF org SKIP LOCKED
            """).ToListAsync(token);
        if (organizations.SingleOrDefault() is not { } org) return;
        var running = db.ProcessRuns.Where(run => run.OrganizationId == org.Id && run.Status == "running");
        var runs = await running.Where(run => run.DueAt <= now && run.EscalatedAt == null)
            .OrderBy(run => run.DueAt).ThenBy(run => run.Id).Take(50).ToListAsync(token);
        var tasks = await (from task in db.ProcessTasks
            join run in running on new { task.OrganizationId, Id = task.RunId } equals new { run.OrganizationId, run.Id }
            where task.CompletedAt == null &&
                (task.DueAt <= now && task.EscalatedAt == null ||
                 task.DueAt > now && task.WarnAt <= now && task.WarnedAt == null)
            orderby task.DueAt, task.RunId, task.StepId
            select new { Task = task, Run = run }).Take(50).ToListAsync(token);

        foreach (var run in runs)
        {
            run.EscalatedAt = now;
            var process = await db.Processes.FindAsync([org.Id, run.ProcessId], token);
            var title = process is null ? "Process" : ProcessSpec.Read(process.Published ?? process.Draft).Name ?? "Process";
            var policy = ApprovalSla.Read(run.SlaSnapshot);
            Record(db, run, "processOverdue", title, policy?.Action == "mark" ? null :
                await RecipientAsync(db, org.Id, run.Subject, policy?.Action, token), now);
        }
        foreach (var entry in tasks)
        {
            var (task, run) = (entry.Task, entry.Run);
            if (task.DueAt > now)
            {
                task.WarnedAt = now;
                Record(db, run, "processReminder", task.Title, task.Assignee, now, task.StepId);
                continue;
            }
            task.EscalatedAt = now;
            var policy = ApprovalSla.Read(run.SlaSnapshot);
            if (policy is { Action: "reassign", Fallback: { } fallback } && fallback != task.Assignee &&
                await ProcessPermissions.FindAsync(db, org.Id, fallback, token) is { Grant.Run: true })
            {
                task.Assignee = fallback;
                Record(db, run, "processTaskReassigned", task.Title, null, now, task.StepId);
                Record(db, run, "processTaskOverdue", task.Title, fallback, now, task.StepId);
                continue;
            }
            Record(db, run, "processTaskOverdue", task.Title, policy?.Action == "mark" ? null :
                await RecipientAsync(db, org.Id, task.Assignee, policy?.Action, token), now, task.StepId);
        }
        await db.SaveChangesAsync(token);
        await transaction.CommitAsync(token);
    }

    private static async Task<string> RecipientAsync(FlowOpsDb db, Guid org, string subject, string? action, CancellationToken token)
    {
        var member = await db.Memberships.FindAsync([org, subject], token);
        if (member is not null && action == "supervisor" &&
            await OrganizationEndpoints.ResolveManagerAsync(db, member, token) is { } manager) return manager;
        if (member?.DepartmentId is Guid department && action == "department" &&
            await db.OrganizationGroups.Where(group => group.OrganizationId == org && group.Id == department)
                .Select(group => group.Lead).SingleOrDefaultAsync(token) is { } lead) return lead;
        return await db.Memberships.Where(member => member.OrganizationId == org &&
                (member.Role == "OrganizationOwner" || member.Role == "OrganizationAdmin"))
            .OrderBy(member => member.Subject == subject).ThenBy(member => member.Role != "OrganizationOwner")
            .ThenBy(member => member.Subject).Select(member => member.Subject).FirstOrDefaultAsync(token) ?? subject;
    }

    private static void Record(FlowOpsDb db, ProcessRun run, string action, string title, string? recipient, DateTimeOffset now, string? step = null)
    {
        db.OrganizationEvents.Add(new() { OrganizationId = run.OrganizationId, Actor = "system", Action = action,
            Target = step is null ? run.Id.ToString() : $"{run.Id}/{step}", At = now });
        if (recipient is not null && recipient != "webhook")
            db.Notifications.Add(new() { OrganizationId = run.OrganizationId, ProcessId = run.ProcessId, ProcessRunId = run.Id,
                ProcessTitle = title, Kind = action, Recipient = recipient });
    }
}
