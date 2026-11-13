using System.Security.Claims;
using Elsa.Workflows.Runtime;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using System.Text.Json;

namespace FlowOps.Api;

internal static class PurchaseEndpoints
{
    private static readonly string[] Categories = ["equipment", "software", "services", "supplies", "other"];
    private static readonly string[] Priorities = ["low", "normal", "high"];
    private static readonly string[] SubmissionRoles = ["OrganizationOwner", "OrganizationAdmin", "Employee", "Manager", "Approver"];
    internal static readonly string[] ManagerRoles = ["OrganizationOwner", "OrganizationAdmin", "Manager"];

    public static void MapFlowOps(this WebApplication app)
    {
        var api = app.MapGroup("/api").RequireAuthorization();
        api.MapGet("/me", async (ClaimsPrincipal user, FlowOpsDb db, IConfiguration config, CancellationToken token) =>
        {
            var subject = Subject(user);
            var memberships = await (from member in db.Memberships
                join org in db.Organizations on member.OrganizationId equals org.Id
                where member.Subject == subject
                orderby org.Name
                select new { org.Id, org.Name, org.Slug, org.Currency, member.Role }).ToListAsync(token);
            return Results.Ok(new { Name = user.FindFirstValue("name") ?? user.FindFirstValue("preferred_username"),
                Organizations = memberships, IsOperator = ReportingEndpoints.IsOperator(user, config) });
        });
        api.MapPost("/organizations", CreateOrganizationAsync);
        var tenant = api.MapGroup("/organizations/{organizationId:guid}");
        OrganizationEndpoints.Map(api, tenant);
        ReportingEndpoints.Map(api, tenant);
        WorkflowCatalog.Map(tenant);
        WorkflowSimulationEndpoints.Map(tenant);
        ProcessEndpoints.Map(tenant);
        StudioFormEndpoints.Map(tenant);
        ApprovalAssignments.Map(tenant);
        ApprovalDelegations.Map(tenant);
        ApprovalMatrix.Map(tenant);
        ApprovalSla.Map(tenant);
        tenant.MapGet("/workspace", GetWorkspaceAsync);
        tenant.MapGet("/tasks", GetTasksAsync);
        tenant.MapPost("/purchases", SubmitAsync);
        tenant.MapGet("/purchases/{purchaseId:guid}", GetPurchaseAsync);
        tenant.MapPost("/purchases/{purchaseId:guid}/cancel", CancelAsync);
        tenant.MapPost("/purchases/{purchaseId:guid}/approvals/{stage:int}", DecideAsync);
        tenant.MapPost("/notifications/{notificationId:guid}/read", MarkReadAsync);
    }

    internal static string Subject(ClaimsPrincipal user) => user.FindFirstValue("sub") ??
        throw new InvalidOperationException("An authenticated subject is required.");

    private static Task<Membership?> MembershipAsync(FlowOpsDb db, Guid organizationId, string subject, CancellationToken token) =>
        db.Memberships.SingleOrDefaultAsync(x => x.OrganizationId == organizationId && x.Subject == subject, token);

    private static IResult Invalid(string field, string message) =>
        Results.ValidationProblem(new Dictionary<string, string[]> { [field] = [message] });

    private static async Task<IResult> CreateOrganizationAsync(
        OrganizationInput input, ClaimsPrincipal user, FlowOpsDb db, CancellationToken token)
    {
        var name = input.Name?.Trim();
        var slug = input.Slug?.Trim().ToLowerInvariant();
        if (name is null || name.Length is < 2 or > 100) return Invalid("name", "Use 2 to 100 characters.");
        if (slug is null || slug.Length is < 2 or > 48 ||
            !char.IsAsciiLetterOrDigit(slug[0]) || !char.IsAsciiLetterOrDigit(slug[^1]) ||
            slug.Any(c => !char.IsAsciiLetterOrDigit(c) && c != '-'))
            return Invalid("slug", "Use 2 to 48 ASCII letters, digits, or internal hyphens.");
        var id = Guid.NewGuid();
        db.Organizations.Add(new Organization { Id = id, Name = name, Slug = slug });
        db.Memberships.Add(new Membership { OrganizationId = id, Subject = Subject(user), Role = "OrganizationOwner",
            Email = OrganizationEndpoints.VerifiedEmail(user) });
        try
        {
            await db.SaveChangesAsync(token);
        }
        catch (DbUpdateException exception) when (exception.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation })
        {
            return Results.Conflict(new { code = "slugTaken" });
        }
        return Results.Created($"/api/organizations/{id}/workspace", new { Id = id, Name = name, Slug = slug });
    }

    private static async Task<IResult> GetWorkspaceAsync(Guid organizationId, int? page, int? notificationPage, int? months,
        ClaimsPrincipal user, FlowOpsDb db, CancellationToken token)
    {
        var subject = Subject(user);
        var member = await MembershipAsync(db, organizationId, subject, token);
        if (member is null) return Results.NotFound();
        const int pageSize = 10;
        var pageNumber = Math.Clamp(page ?? 1, 1, 10000);
        var currentNotifications = Math.Clamp(notificationPage ?? 1, 1, 10000);
        var owned = db.Purchases.AsNoTracking().Where(x => x.OrganizationId == organizationId && x.Requester == subject);
        var pending = PersonalApprovals(db, organizationId, subject).Where(x => x.Decision == null);
        var pendingCount = await pending.CountAsync(token);
        var statuses = await owned.GroupBy(x => x.Status).Select(x => new { Status = x.Key, Count = x.Count() }).ToListAsync(token);
        var purchases = await owned.OrderByDescending(x => x.CreatedAt).ThenBy(x => x.Id).Skip((pageNumber - 1) * pageSize).Take(pageSize)
            .Select(x => new { x.Id, x.Title, x.Category, x.Amount, x.Currency, x.Status, x.CreatedAt }).ToListAsync(token);
        var queue = await (from approval in pending
            join purchase in db.Purchases on new { approval.OrganizationId, Id = approval.PurchaseId } equals new { purchase.OrganizationId, purchase.Id }
            orderby purchase.CreatedAt, purchase.Id
            select new { purchase.Id, purchase.Title, purchase.Category, purchase.Amount, purchase.Currency, purchase.Status, purchase.CreatedAt, approval.Stage })
            .Skip((pageNumber - 1) * pageSize).Take(pageSize).ToListAsync(token);
        var notifications = await (from notification in db.Notifications.AsNoTracking()
            join purchase in db.Purchases on new { notification.OrganizationId, Id = notification.PurchaseId } equals new { purchase.OrganizationId, Id = (Guid?)purchase.Id } into purchasesForNotice
            from purchase in purchasesForNotice.DefaultIfEmpty()
            where notification.OrganizationId == organizationId && notification.Recipient == subject
            orderby notification.CreatedAt descending, notification.Id
            select new { notification.Id, notification.PurchaseId, Title = purchase == null ? notification.ProcessTitle : purchase.Title,
                notification.ProcessId, notification.ProcessRunId, notification.Kind, notification.CreatedAt, notification.ReadAt })
            .Skip((currentNotifications - 1) * pageSize).Take(pageSize + 1).ToListAsync(token);
        var now = DateTimeOffset.UtcNow;
        var monthStart = new DateTimeOffset(now.Year, now.Month, 1, 0, 0, 0, TimeSpan.Zero);
        var period = months is 6 or 12 or 48 ? months.Value : 0;
        var since = monthStart.AddMonths(1 - period);
        var activity = period == 0 ? [] : await owned
            .Where(x => x.CreatedAt >= since && x.CreatedAt <= now)
            .GroupBy(x => new { x.CreatedAt.Year, x.CreatedAt.Month, x.Status })
            .Select(group => new MonthlyActivity(group.Key.Year, group.Key.Month, group.Key.Status, group.Count()))
            .ToListAsync(token);
        var canSubmit = SubmissionRoles.Contains(member.Role) && await OrganizationEndpoints.ResolveManagerAsync(db, member, token) is not null;
        return Results.Ok(new { Purchases = purchases, Approvals = queue, PendingCount = pendingCount,
            Statuses = statuses, Notifications = notifications.Take(pageSize), NotificationPage = currentNotifications,
            NotificationHasMore = notifications.Count > pageSize, Activity = activity, AsOf = now,
            CanSubmit = canSubmit, Page = pageNumber, PageSize = pageSize });
    }

    private static async Task<IResult> GetTasksAsync(Guid organizationId, string? status, string? priority, int? page,
        ClaimsPrincipal user, FlowOpsDb db, CancellationToken token)
    {
        var subject = Subject(user);
        if (await MembershipAsync(db, organizationId, subject, token) is null) return Results.NotFound();
        if (status is not (null or "pending" or "overdue" or "completed") ||
            (priority is not null && !Priorities.Contains(priority)))
            return Results.BadRequest(new { code = "invalidFilter" });

        var now = DateTimeOffset.UtcNow;
        var approvals = PersonalApprovals(db, organizationId, subject);
        approvals = status switch
        {
            "completed" => approvals.Where(x => x.Decision != null),
            "overdue" => approvals.Where(x => x.Decision == null && x.DueAt <= now),
            _ => approvals.Where(x => x.Decision == null)
        };

        const int pageSize = 10;
        var current = Math.Clamp(page ?? 1, 1, 10000);
        var query = from approval in approvals
            join purchase in db.Purchases.AsNoTracking()
                on new { approval.OrganizationId, Id = approval.PurchaseId } equals new { purchase.OrganizationId, purchase.Id }
            where priority == null || purchase.Priority == priority
            select new { purchase.Id, purchase.Title, purchase.Priority, approval.Stage,
                approval.Decision, approval.DueAt, approval.DecidedAt,
                Overdue = approval.Decision == null && approval.DueAt <= now };
        var ordered = status == "completed" ? query.OrderByDescending(x => x.DecidedAt) :
            query.OrderByDescending(x => x.Overdue).ThenBy(x => x.Priority == "high" ? 0 : x.Priority == "normal" ? 1 : 2).ThenBy(x => x.DueAt);
        var items = await ordered.ThenBy(x => x.Id).ThenBy(x => x.Stage)
            .Skip((current - 1) * pageSize).Take(pageSize + 1).ToListAsync(token);
        return Results.Ok(new { Items = items.Take(pageSize), Page = current, HasMore = items.Count > pageSize });
    }

    private static IQueryable<Approval> PersonalApprovals(FlowOpsDb db, Guid org, string subject)
    {
        var votes = db.ApprovalVotes.Where(vote => vote.OrganizationId == org);
        return db.Approvals.AsNoTracking().Where(approval => approval.OrganizationId == org &&
            (approval.Assignee == subject && !votes.Any(vote => vote.PurchaseId == approval.PurchaseId && vote.Stage == approval.Stage) ||
             votes.Any(vote => vote.PurchaseId == approval.PurchaseId && vote.Stage == approval.Stage &&
                vote.Subject == subject && (approval.Decision != null || vote.Decision == null))));
    }

    private static async Task<IResult> SubmitAsync(Guid organizationId, PurchaseInput input, ClaimsPrincipal user, FlowOpsDb db, CancellationToken token)
    {
        var subject = Subject(user);
        await using var transaction = await db.Database.BeginTransactionAsync(token);
        var org = await OrganizationEndpoints.LockAsync(db, organizationId, token);
        var member = await MembershipAsync(db, organizationId, subject, token);
        if (member is null || org is null) return Results.NotFound();
        if (!SubmissionRoles.Contains(member.Role)) return Results.StatusCode(403);
        var title = input.Title?.Trim();
        var description = input.Description?.Trim();
        if (input.Id == Guid.Empty) return Invalid("id", "A request ID is required.");
        if (title is null || title.Length is < 3 or > 120) return Invalid("title", "Use 3 to 120 characters.");
        if (description is null || description.Length is < 5 or > 2000) return Invalid("description", "Use 5 to 2,000 characters.");
        if (input.Amount is < 0.01m or > 999_999_999.99m || decimal.Round(input.Amount, 2) != input.Amount)
            return Invalid("amount", "Enter a positive amount with at most two decimal places.");
        if (input.Category is null || !Categories.Contains(input.Category)) return Invalid("category", "Select a category.");
        if (input.Currency != org.Currency) return Invalid("currency", "Select the organization currency.");
        if (input.Priority is not null && !Priorities.Contains(input.Priority)) return Invalid("priority", "Select a priority.");
        var existing = await db.Purchases.AsNoTracking().SingleOrDefaultAsync(x => x.OrganizationId == organizationId && x.Id == input.Id, token);
        if (existing is not null) return Replay(existing, input, subject, title, description);

        if (input.PreviousPurchaseId is Guid previous)
        {
            var original = await db.Purchases.AsNoTracking().SingleOrDefaultAsync(row =>
                row.OrganizationId == organizationId && row.Id == previous && row.Requester == subject, token);
            if (original is null) return Results.NotFound();
            if (original.Status != "changesRequested" || await db.Purchases.AnyAsync(row =>
                row.OrganizationId == organizationId && row.PreviousPurchaseId == previous, token))
                return Results.Conflict(new { code = "revisionUnavailable" });
            if (original.WorkflowId != input.WorkflowId)
                return Invalid("workflowId", "Use the original workflow when submitting a revision.");
        }

        PurchaseWorkflowVersion? version = null;
        if (input.WorkflowId is Guid workflowId)
        {
            version = await WorkflowCatalog.PublishedAsync(db, organizationId, workflowId, token);
            if (version is null) return Results.NotFound();
            if (version.Number != input.WorkflowVersion) return Results.Conflict(new { code = "workflowChanged" });
        }
        else if (input.WorkflowVersion is not null) return Invalid("workflowVersion", "Select a published workflow.");

        var manager = await OrganizationEndpoints.ResolveManagerAsync(db, member, token);
        if (manager is null || manager == subject || !await db.Memberships.AnyAsync(x =>
            x.OrganizationId == organizationId && x.Subject == manager && ManagerRoles.Contains(x.Role), token))
            return Results.Conflict(new { code = "managerMissing" });
        string? finance = null;
        var graph = version is null ? null : PurchaseGraph.Read(version.GraphJson);
        var matrix = version is null ? org.ApprovalMatrixJson : version.ApprovalMatrixSnapshot;
        var rule = ApprovalMatrix.Match(ApprovalMatrix.Read(matrix), org.Currency, input.Amount,
            input.Category, member.Role, member.DepartmentId);
        var reviewers = rule?.AdditionalReviewers ?? [];
        if (reviewers.Contains(subject) || reviewers.Contains(manager) || reviewers.Length != await db.Memberships.CountAsync(row =>
            row.OrganizationId == organizationId && reviewers.Contains(row.Subject) && ManagerRoles.Contains(row.Role), token))
            return Results.Conflict(new { code = "reviewersUnavailable" });
        var needsFinance = (graph is null || graph.Nodes!.Any(node => node?.Kind == "finance")) && (rule?.Route switch
        {
            "manager" => false,
            "managerFinance" => true,
            _ => input.Amount > (version?.FinanceThreshold ?? org.FinanceThreshold)
        });
        if (needsFinance)
        {
            finance = await db.Memberships.Where(x => x.OrganizationId == organizationId && x.Role == "Approver" &&
                    x.Subject != subject && x.Subject != manager && !reviewers.Contains(x.Subject) &&
                    (org.FinanceApprover == null || x.Subject == org.FinanceApprover))
                .OrderBy(x => x.Subject).Select(x => x.Subject).FirstOrDefaultAsync(token);
            if (finance is null) return Results.Conflict(new { code = "financeMissing" });
        }

        var purchase = new Purchase { OrganizationId = organizationId, Id = input.Id, Requester = subject, Title = title,
            Description = description, Category = input.Category, Amount = input.Amount, Currency = org.Currency, Manager = manager,
            Finance = finance, WorkflowId = version?.WorkflowId, WorkflowVersion = version?.Number, Priority = input.Priority ?? "normal",
            ReviewersJson = reviewers.Length == 0 ? null : JsonSerializer.Serialize(reviewers), MinimumApprovals = rule?.MinimumApprovals,
            PreviousPurchaseId = input.PreviousPurchaseId, WorkflowDueAt = ApprovalSla.WorkflowDeadline(org.ApprovalSlaJson, DateTimeOffset.UtcNow) };
        db.Purchases.Add(purchase);
        db.WorkflowCommands.Add(new WorkflowCommand { OrganizationId = organizationId, PurchaseId = purchase.Id, Stage = 0 });
        Record(db, purchase, subject, "submitted");
        try
        {
            await db.SaveChangesAsync(token);
        }
        catch (DbUpdateException exception) when (exception.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation })
        {
            db.ChangeTracker.Clear();
            existing = await db.Purchases.AsNoTracking().SingleAsync(x => x.OrganizationId == organizationId && x.Id == input.Id, token);
            return Replay(existing, input, subject, title, description);
        }
        await transaction.CommitAsync(token);
        return Results.Created($"/api/organizations/{organizationId}/purchases/{purchase.Id}", new { purchase.Id });
    }

    private static IResult Replay(Purchase existing, PurchaseInput input, string subject, string title, string description) =>
        existing.Requester == subject && existing.Title == title && existing.Description == description &&
        existing.Category == input.Category && existing.Amount == input.Amount && existing.Currency == input.Currency &&
        existing.WorkflowId == input.WorkflowId && existing.WorkflowVersion == input.WorkflowVersion &&
        existing.Priority == (input.Priority ?? "normal") && existing.PreviousPurchaseId == input.PreviousPurchaseId
            ? Results.Ok(new { existing.Id })
            : Results.Conflict(new { code = "requestConflict" });

    private static async Task<IResult> GetPurchaseAsync(Guid organizationId, Guid purchaseId, ClaimsPrincipal user, FlowOpsDb db,
        IActivityExecutionStore executions, CancellationToken token)
    {
        var subject = Subject(user);
        var member = await MembershipAsync(db, organizationId, subject, token);
        if (member is null) return Results.NotFound();
        var purchase = await db.Purchases.AsNoTracking().SingleOrDefaultAsync(x => x.OrganizationId == organizationId && x.Id == purchaseId, token);
        if (purchase is null) return Results.NotFound();
        var approvals = db.Approvals.AsNoTracking().Where(x => x.OrganizationId == organizationId && x.PurchaseId == purchaseId);
        var canManage = OrganizationEndpoints.CanManage(member.Role);
        var votes = await db.ApprovalVotes.AsNoTracking().Where(x => x.OrganizationId == organizationId && x.PurchaseId == purchaseId)
            .OrderBy(x => x.Subject).ToListAsync(token);
        if (!canManage && purchase.Requester != subject && !votes.Any(vote => vote.Subject == subject) &&
            !await approvals.AnyAsync(x => x.Assignee == subject, token))
            return Results.NotFound();
        var workflow = await db.PurchaseWorkflowVersions.AsNoTracking().Where(x => x.OrganizationId == organizationId &&
            x.WorkflowId == purchase.WorkflowId && x.Number == purchase.WorkflowVersion)
            .Select(x => new { x.Name, x.Number, x.GraphJson, x.ElsaDefinitionId }).SingleOrDefaultAsync(token);
        var graph = workflow?.ElsaDefinitionId is null ? null : PurchaseGraph.Read(workflow.GraphJson);
        var records = graph is null ? [] : (await executions.FindManyAsync(new()
        {
            WorkflowInstanceId = PurchaseWorkflowWorker.InstanceId(organizationId, purchaseId),
            ActivityIds = graph.Nodes!.Select(node => node!.Id!).ToArray()
        }, token)).ToArray();
        var execution = graph is null ? null : new
        {
            Graph = graph,
            Nodes = records.GroupBy(record => record.ActivityId).Select(group => group.OrderByDescending(record => record.StartedAt).First())
                .Select(record => new { Id = record.ActivityId, Status = record.Status.ToString(), record.CompletedAt })
        };
        var timeline = await db.Audit.AsNoTracking().Where(x => x.OrganizationId == organizationId && x.PurchaseId == purchaseId)
            .OrderBy(x => x.At).Select(x => new { x.Action, x.At }).ToListAsync(token);
        var rows = await approvals.OrderBy(x => x.Stage).ToListAsync(token);
        var stages = rows.Select(x => new { x.Stage, x.Assignee, x.Decision, x.Comment, x.DecidedAt, x.DueAt,
            Parallel = x.Stage == 1 && votes.Count > 0,
            CanDecide = x.Decision == null && (x.Stage == 1 && votes.Count > 0
                ? votes.Any(vote => vote.Subject == subject && vote.Decision is null)
                : x.Assignee == subject) }).ToArray();
        var assignmentHistory = await db.ApprovalAssignments.AsNoTracking()
            .Where(x => x.OrganizationId == organizationId && x.PurchaseId == purchaseId).OrderBy(x => x.At).ThenBy(x => x.Id)
            .Select(x => new { x.Stage, x.Assignee, x.Actor, x.At }).ToListAsync(token);
        var managerRoles = ManagerRoles;
        var activeStages = stages.Where(x => x.CanDecide).Select(x => x.Stage).ToArray();
        var candidates = canManage || activeStages.Length > 0 ? await db.Memberships.AsNoTracking().Where(x =>
            x.OrganizationId == organizationId && x.Subject != purchase.Requester &&
            ((canManage || activeStages.Contains(1)) && managerRoles.Contains(x.Role) ||
             (canManage || activeStages.Contains(2)) && x.Role == "Approver"))
            .OrderBy(x => x.Email).ThenBy(x => x.Subject).Select(x => new { x.Subject, x.Email, x.Role }).ToListAsync(token) : null;
        var revised = purchase.Status == "changesRequested" ? await db.Purchases.Where(row =>
            row.OrganizationId == organizationId && row.PreviousPurchaseId == purchaseId).Select(row => (Guid?)row.Id).SingleOrDefaultAsync(token) : null;
        return Results.Ok(new { purchase.Id, purchase.Title, purchase.Description, purchase.Category, purchase.PreviousPurchaseId,
            RevisedPurchaseId = revised, CanResubmit = purchase.Requester == subject && purchase.Status == "changesRequested" && revised is null,
            purchase.WorkflowId, CanCancel = purchase.Requester == subject && stages.Any(stage => stage.Decision is null),
            purchase.Amount, purchase.Currency, purchase.Status, purchase.CreatedAt, purchase.Priority, purchase.WorkflowDueAt,
            Workflow = workflow is null ? null : new { workflow.Name, workflow.Number }, Execution = execution,
            Timeline = timeline, Stages = stages, AssignmentHistory = assignmentHistory,
            EligibleMembers = canManage ? candidates : null, DelegationMembers = candidates,
            MinimumApprovals = purchase.MinimumApprovals, Votes = votes.Select(vote => new
            { vote.Subject, vote.Decision, vote.DecidedAt, Mine = vote.Subject == subject }),
            Processing = purchase.Status == "starting" ||
                (purchase.Status == "managerReview" && stages.Any(x => x.Stage == 1 && x.Decision != null)) ||
                (purchase.Status == "financeReview" && stages.Any(x => x.Stage == 2 && x.Decision != null)) });
    }

    private static async Task<IResult> CancelAsync(Guid organizationId, Guid purchaseId, ClaimsPrincipal user,
        FlowOpsDb db, CancellationToken token)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(token);
        if (await OrganizationEndpoints.LockAsync(db, organizationId, token) is null) return Results.NotFound();
        var subject = Subject(user);
        if (await MembershipAsync(db, organizationId, subject, token) is null) return Results.NotFound();
        var purchase = await db.Purchases.FindAsync([organizationId, purchaseId], token);
        if (purchase is null || purchase.Requester != subject) return Results.NotFound();
        if (purchase.Status == "canceled") return Results.Accepted();
        if (purchase.Status is not ("managerReview" or "financeReview")) return Results.Conflict();
        var now = DateTimeOffset.UtcNow;
        var closed = await db.Approvals.Where(row => row.OrganizationId == organizationId && row.PurchaseId == purchaseId &&
            row.Decision == null).ExecuteUpdateAsync(set => set.SetProperty(row => row.Decision, "canceled")
                .SetProperty(row => row.DecidedAt, now), token);
        if (closed != 1) return Results.Conflict();
        purchase.Status = "canceled";
        Record(db, purchase, subject, "canceled");
        db.WorkflowCommands.Add(new() { OrganizationId = organizationId, PurchaseId = purchaseId, Stage = -1 });
        await db.SaveChangesAsync(token);
        await transaction.CommitAsync(token);
        return Results.Accepted();
    }

    private static async Task<IResult> DecideAsync(Guid organizationId, Guid purchaseId, int stage, DecisionInput input,
        ClaimsPrincipal user, FlowOpsDb db, CancellationToken token)
    {
        var subject = Subject(user);
        var member = await MembershipAsync(db, organizationId, subject, token);
        if (member is null) return Results.NotFound();
        if (input.Decision is not ("approved" or "rejected" or "changesRequested")) return Invalid("decision", "Select a decision.");
        var comment = input.Comment?.Trim();
        if (comment?.Length > 1000 || input.Decision == "changesRequested" && string.IsNullOrEmpty(comment))
            return Invalid("comment", "Enter a reason for requested changes, up to 1,000 characters.");
        var purchase = await db.Purchases.SingleOrDefaultAsync(x => x.OrganizationId == organizationId && x.Id == purchaseId, token);
        if (purchase?.ReviewersJson is not null && stage == 1)
            return await DecideParallelAsync(db, purchase, subject, input.Decision, comment, token);
        var approval = await db.Approvals.SingleOrDefaultAsync(x => x.OrganizationId == organizationId && x.PurchaseId == purchaseId && x.Stage == stage, token);
        if (purchase is null || approval is null || approval.Assignee != subject) return Results.NotFound();
        if (purchase.Requester == subject || (stage == 1 && !ManagerRoles.Contains(member.Role)) ||
            (stage == 2 && member.Role != "Approver")) return Results.StatusCode(403);
        if (approval.Decision is not null) return DecisionReplay(approval, input.Decision, comment);
        approval.Decision = input.Decision;
        approval.Comment = comment;
        approval.DecidedAt = DateTimeOffset.UtcNow;
        Record(db, purchase, subject, stage == 1 ? $"manager-{input.Decision}" : $"finance-{input.Decision}");
        db.WorkflowCommands.Add(new WorkflowCommand { OrganizationId = organizationId, PurchaseId = purchaseId, Stage = stage });
        try
        {
            await db.SaveChangesAsync(token);
        }
        catch (DbUpdateConcurrencyException)
        {
            await db.Entry(approval).ReloadAsync(token);
            if (approval.Assignee != subject) return Results.NotFound();
            return DecisionReplay(approval, input.Decision, comment);
        }
        catch (DbUpdateException exception) when (exception.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation })
        {
            await db.Entry(approval).ReloadAsync(token);
            return DecisionReplay(approval, input.Decision, comment);
        }
        return Results.Accepted();
    }

    private static async Task<IResult> DecideParallelAsync(FlowOpsDb db, Purchase purchase, string subject,
        string decision, string? comment, CancellationToken token)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(token);
        var org = purchase.OrganizationId;
        if (await OrganizationEndpoints.LockAsync(db, org, token) is null) return Results.NotFound();
        var approval = (await db.Approvals.FromSqlInterpolated($"""
            SELECT * FROM flowops."Approvals"
            WHERE "OrganizationId" = {org} AND "PurchaseId" = {purchase.Id} AND "Stage" = 1 FOR UPDATE
            """).ToListAsync(token)).SingleOrDefault();
        if (approval is null) return Results.NotFound();
        var votes = await db.ApprovalVotes.Where(x => x.OrganizationId == org && x.PurchaseId == purchase.Id && x.Stage == 1).ToListAsync(token);
        var vote = votes.SingleOrDefault(x => x.Subject == subject);
        if (vote is null) return Results.NotFound();
        if (subject == purchase.Requester || !await db.Memberships.AnyAsync(x => x.OrganizationId == org &&
            x.Subject == subject && ManagerRoles.Contains(x.Role), token)) return Results.StatusCode(403);
        if (vote.Decision is not null)
            return vote.Decision == decision && vote.Comment == comment ? Results.Accepted() : Results.Conflict(new { code = "alreadyDecided" });
        if (approval.Decision is not null) return Results.Conflict(new { code = "alreadyDecided" });
        vote.Decision = decision;
        vote.Comment = comment;
        vote.DecidedAt = DateTimeOffset.UtcNow;
        Record(db, purchase, subject, $"vote-{decision}");
        var accepted = votes.Count(x => x.Decision == "approved");
        var outstanding = votes.Count(x => x.Decision is null);
        var minimum = purchase.MinimumApprovals!.Value;
        if (decision == "changesRequested" || accepted >= minimum || accepted + outstanding < minimum)
        {
            approval.Decision = decision == "changesRequested" ? decision : accepted >= minimum ? "approved" : "rejected";
            if (decision == "changesRequested") approval.Comment = comment;
            approval.DecidedAt = vote.DecidedAt;
            Record(db, purchase, "system", $"manager-{approval.Decision}");
            db.WorkflowCommands.Add(new() { OrganizationId = org, PurchaseId = purchase.Id, Stage = 1 });
        }
        await db.SaveChangesAsync(token);
        await transaction.CommitAsync(token);
        return Results.Accepted();
    }

    private static IResult DecisionReplay(Approval approval, string decision, string? comment) =>
        approval.Decision == decision && approval.Comment == comment ? Results.Accepted() : Results.Conflict(new { code = "alreadyDecided" });

    private static async Task<IResult> MarkReadAsync(Guid organizationId, Guid notificationId, ClaimsPrincipal user, FlowOpsDb db, CancellationToken token)
    {
        var subject = Subject(user);
        if (await MembershipAsync(db, organizationId, subject, token) is null) return Results.NotFound();
        var count = await db.Notifications.Where(x => x.OrganizationId == organizationId && x.Recipient == subject && x.Id == notificationId)
            .ExecuteUpdateAsync(set => set.SetProperty(x => x.ReadAt, x => x.ReadAt ?? DateTimeOffset.UtcNow), token);
        return count == 1 ? Results.NoContent() : Results.NotFound();
    }

    internal static void Record(FlowOpsDb db, Purchase purchase, string actor, string action) =>
        db.Audit.Add(new AuditEvent { OrganizationId = purchase.OrganizationId, PurchaseId = purchase.Id, Actor = actor,
            Action = action, CorrelationId = PurchaseWorkflowWorker.InstanceId(purchase.OrganizationId, purchase.Id) });
}

internal sealed record OrganizationInput(string? Name, string? Slug);
internal sealed record PurchaseInput(Guid Id, string? Title, string? Description, string? Category, string? Currency, decimal Amount,
    Guid? WorkflowId = null, int? WorkflowVersion = null, string? Priority = null, Guid? PreviousPurchaseId = null);
internal sealed record DecisionInput(string? Decision, string? Comment);
internal sealed record MonthlyActivity(int Year, int Month, string Status, int Count);
