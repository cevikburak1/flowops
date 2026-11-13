using System.Net.Http.Json;
using System.Net.Mail;
using System.Text.Json;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using RabbitMQ.Client;

namespace FlowOps.Api;

internal sealed class NotificationDispatcher(
    IServiceScopeFactory scopes, IConfiguration config, IHttpClientFactory http, IHubContext<NotificationHub> hub,
    ILogger<NotificationDispatcher> logger) : BackgroundService
{
    internal static readonly string[] Channels = ["realtime", "rabbitmq", "email", "webhook"];
    private const string Queue = "flowops.notifications";
    private IConnection? rabbitConnection;
    private IChannel? rabbitPublisher;

    protected override Task ExecuteAsync(CancellationToken token) =>
        RunAsync(token);

    private async Task RunAsync(CancellationToken token)
    {
        try { await Task.WhenAll(Channels.Select(channel => RunChannelAsync(channel, token))); }
        finally
        {
            if (rabbitPublisher is not null) await rabbitPublisher.DisposeAsync();
            if (rabbitConnection is not null) await rabbitConnection.DisposeAsync();
        }
    }

    private bool Enabled(string channel) => channel switch
    {
        "rabbitmq" => !string.IsNullOrEmpty(config["Integrations:RabbitMq"]),
        "email" => !string.IsNullOrEmpty(config["Integrations:Smtp:Host"]),
        "webhook" => !string.IsNullOrEmpty(config["Integrations:Webhook:Url"]),
        _ => true
    };

    private async Task RunChannelAsync(string channel, CancellationToken token)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(1));
        while (await timer.WaitForNextTickAsync(token))
        {
            try
            {
                for (var count = 0; count < 25 && await DeliverNextAsync(channel, token); count++) { }
            }
            catch (Exception exception) when (!token.IsCancellationRequested)
            {
                logger.LogError(exception, "Could not process {Channel} notification queue", channel);
            }
        }
    }

    private async Task<bool> DeliverNextAsync(string channel, CancellationToken token)
    {
        await using var scope = scopes.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<FlowOpsDb>();
        await using var transaction = await db.Database.BeginTransactionAsync(token);
        var now = DateTimeOffset.UtcNow;
        var candidates = await db.NotificationDeliveries.FromSqlInterpolated($"""
            SELECT * FROM flowops."NotificationDeliveries"
            WHERE "Channel" = {channel} AND "CompletedAt" IS NULL AND "NextAttemptAt" <= {now}
            ORDER BY "NextAttemptAt", "NotificationId" LIMIT 1 FOR UPDATE SKIP LOCKED
            """).ToListAsync(token);
        if (candidates.SingleOrDefault() is not { } delivery) return false;
        var notification = await db.Notifications.SingleAsync(x => x.Id == delivery.NotificationId, token);
        var member = await db.Memberships.AsNoTracking().SingleOrDefaultAsync(x =>
            x.OrganizationId == notification.OrganizationId && x.Subject == notification.Recipient, token);
        var payload = new NotificationEvent(notification.Id, notification.OrganizationId, notification.PurchaseId,
            notification.Kind, notification.CreatedAt);
        try
        {
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(token);
            timeout.CancelAfter(TimeSpan.FromSeconds(12));
            if (member is not null && Enabled(channel))
                await SendAsync(db, member, channel, payload, timeout.Token);
            delivery.CompletedAt = DateTimeOffset.UtcNow;
        }
        catch (Exception exception) when (!token.IsCancellationRequested)
        {
            delivery.Attempts++;
            delivery.NextAttemptAt = now.AddSeconds(Math.Min(900, 1 << Math.Min(delivery.Attempts, 10)));
            logger.LogWarning("Notification {NotificationId} delivery to {Channel} failed ({ErrorType}), attempt {Attempt}",
                notification.Id, channel, exception.GetType().Name, delivery.Attempts);
        }
        await db.SaveChangesAsync(token);
        await transaction.CommitAsync(token);
        return true;
    }

    private async Task SendAsync(FlowOpsDb db, Membership member, string channel, NotificationEvent payload, CancellationToken token)
    {
        switch (channel)
        {
            case "realtime":
                await hub.Clients.User(member.Subject)
                    .SendAsync("NotificationsChanged", cancellationToken: token);
                break;
            case "rabbitmq":
                await PublishAsync(payload, token);
                break;
            case "email" when member.Email is not null:
                var smtp = config.GetSection("Integrations:Smtp");
                using (var client = SmtpDelivery.CreateClient(smtp))
                using (var mail = new MailMessage(smtp["From"]!, member.Email))
                {
                    mail.Subject = "FlowOps purchase update";
                    var origin = config["Auth:WebOrigin"] ?? "http://localhost:3000";
                    mail.Body = $"Open FlowOps to review this update.\n{origin.TrimEnd('/')}/workspace/{payload.OrganizationId}/purchases/{payload.PurchaseId}";
                    await client.SendMailAsync(mail, token);
                }
                break;
            case "webhook":
                if (!await db.Organizations.AnyAsync(x => x.Id == member.OrganizationId && x.WebhookEnabled, token)) break;
                using (var request = new HttpRequestMessage(HttpMethod.Post, config["Integrations:Webhook:Url"]))
                {
                    request.Headers.Add("Idempotency-Key", payload.Id.ToString());
                    request.Headers.Add("X-FlowOps-Key", config["Integrations:Webhook:Key"]);
                    request.Content = JsonContent.Create(payload);
                    using var response = await http.CreateClient("flowops-webhook").SendAsync(request, HttpCompletionOption.ResponseHeadersRead, token);
                    response.EnsureSuccessStatusCode();
                }
                break;
        }
    }

    private async Task PublishAsync(NotificationEvent payload, CancellationToken token)
    {
        if (rabbitConnection?.IsOpen != true)
        {
            if (rabbitPublisher is not null) await rabbitPublisher.DisposeAsync();
            rabbitPublisher = null;
            if (rabbitConnection is not null) await rabbitConnection.DisposeAsync();
            var factory = new ConnectionFactory { Uri = new Uri(config["Integrations:RabbitMq"]!), AutomaticRecoveryEnabled = false };
            rabbitConnection = await factory.CreateConnectionAsync(token);
        }
        if (rabbitPublisher?.IsOpen != true)
        {
            if (rabbitPublisher is not null) await rabbitPublisher.DisposeAsync();
            rabbitPublisher = await rabbitConnection.CreateChannelAsync(new(true, true), token);
            await rabbitPublisher.QueueDeclareAsync(Queue, durable: true, exclusive: false, autoDelete: false,
                cancellationToken: token);
        }
        await rabbitPublisher.BasicPublishAsync("", Queue, true, new BasicProperties
        {
            ContentType = "application/json", MessageId = payload.Id.ToString(), Persistent = true,
        }, JsonSerializer.SerializeToUtf8Bytes(payload, JsonSerializerOptions.Web), token);
    }

    private sealed record NotificationEvent(Guid Id, Guid OrganizationId, Guid PurchaseId, string Kind, DateTimeOffset CreatedAt);
}
