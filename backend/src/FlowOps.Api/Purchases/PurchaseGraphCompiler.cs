using System.Security.Cryptography;
using System.Text;
using Elsa.Workflows;
using Elsa.Workflows.Activities;
using Elsa.Workflows.Activities.Flowchart.Activities;
using Elsa.Workflows.Activities.Flowchart.Attributes;
using Elsa.Workflows.Activities.Flowchart.Models;
using Elsa.Workflows.Management;
using Elsa.Workflows.Models;
using Microsoft.EntityFrameworkCore;
using FlowEndpoint = Elsa.Workflows.Activities.Flowchart.Models.Endpoint;

namespace FlowOps.Api;

internal static class PurchaseGraphCompiler
{
    public static async Task<string> PublishAsync(PurchaseWorkflowDefinition flow, PurchaseGraph graph, int number,
        IWorkflowDefinitionPublisher publisher, IWorkflowDefinitionStore store, CancellationToken token)
    {
        var digest = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(graph.Serialize())));
        var id = $"flowops-{flow.OrganizationId:N}-{flow.Id:N}-{number}-{digest}";
        var existing = await store.FindAsync(new() { Id = id }, token);
        if (existing?.IsPublished == true) return id;

        var activities = graph.Nodes!.Select(node => node!).ToDictionary(node => node.Id!, BuildActivity);
        var root = new Flowchart { Id = "flowops-root", Activities = activities.Values.ToList(),
            Start = activities[graph.Nodes!.Single(node => node!.Kind == "start")!.Id!] };
        foreach (var edge in graph.Edges!)
            root.Connections.Add(new Connection(new FlowEndpoint(activities[edge!.Source!], edge.Branch ?? "Done"),
                new FlowEndpoint(activities[edge.Target!])));

        var definition = await publisher.NewAsync(root, token);
        definition.Id = id;
        definition.DefinitionId = id;
        definition.Name = flow.Name;
        var result = await publisher.PublishAsync(definition, token);
        if (!result.Succeeded) throw new InvalidOperationException("Elsa rejected the compiled purchase workflow.");
        return id;
    }

    private static IActivity BuildActivity(PurchaseNode node)
    {
        IActivity activity = node.Kind switch
        {
            "start" => new Start(),
            "end" => new Finish(),
            "manager" => new WaitForPurchaseApproval { Stage = new(1) },
            "finance" => new WaitForPurchaseApproval { Stage = new(2) },
            "threshold" => new EvaluatePurchaseBranch { Rule = new("threshold") },
            "decision" => new EvaluatePurchaseBranch { Rule = new("decision") },
            "notify" => new NotifyPurchaseRequester(),
            _ => throw new InvalidOperationException("Unsupported purchase node.")
        };
        activity.Id = node.Id!;
        return activity;
    }
}

[FlowNode("above", "below", "approved", "rejected")]
public sealed class EvaluatePurchaseBranch : Activity
{
    public Input<string> Rule { get; set; } = new("decision");

    protected override async ValueTask ExecuteAsync(ActivityExecutionContext context)
    {
        var (org, id) = PurchaseWorkflowWorker.ParseInstanceId(context.WorkflowExecutionContext.Id);
        await using var scope = context.GetRequiredService<IServiceScopeFactory>().CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<FlowOpsDb>();
        var token = context.CancellationToken;
        var approved = await db.Approvals.AnyAsync(x => x.OrganizationId == org && x.PurchaseId == id &&
            x.Stage == 1 && x.Decision == "approved", token);
        var outcome = approved ? "approved" : "rejected";
        if (context.Get(Rule) == "threshold")
        {
            var needsFinance = approved && await db.Purchases.AnyAsync(x => x.OrganizationId == org && x.Id == id && x.Finance != null, token);
            outcome = needsFinance ? "above" : "below";
        }
        await context.CompleteActivityWithOutcomesAsync(outcome);
    }
}

public sealed class NotifyPurchaseRequester : Activity
{
    protected override async ValueTask ExecuteAsync(ActivityExecutionContext context)
    {
        var (org, id) = PurchaseWorkflowWorker.ParseInstanceId(context.WorkflowExecutionContext.Id);
        await using var scope = context.GetRequiredService<IServiceScopeFactory>().CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<FlowOpsDb>();
        var key = new Guid(SHA256.HashData(Encoding.UTF8.GetBytes($"purchase-notice:{org:N}:{id:N}")).AsSpan(0, 16));
        await db.Database.ExecuteSqlInterpolatedAsync($"""
            WITH inserted AS (
                INSERT INTO flowops."Notifications" ("Id", "OrganizationId", "PurchaseId", "Recipient", "Kind", "CreatedAt")
                SELECT {key}, "OrganizationId", "Id", "Requester", 'workflowNotice', now()
                FROM flowops."Purchases" WHERE "OrganizationId" = {org} AND "Id" = {id}
                ON CONFLICT ("Id") DO NOTHING
                RETURNING "Id"
            )
            INSERT INTO flowops."NotificationDeliveries" ("NotificationId", "Channel", "NextAttemptAt", "Attempts")
            SELECT "Id", channel, now(), 0 FROM inserted CROSS JOIN unnest({NotificationDispatcher.Channels}) AS channel
            """, context.CancellationToken);
        await context.CompleteActivityAsync();
    }
}
