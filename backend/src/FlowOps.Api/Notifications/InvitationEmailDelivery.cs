using System.Net.Mail;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;

namespace FlowOps.Api;

internal sealed class InvitationEmailDelivery(IServiceScopeFactory scopes, IConfiguration configuration,
    IDataProtectionProvider protection, ILogger<InvitationEmailDelivery> logger) : BackgroundService
{
    private IConfigurationSection Smtp => configuration.GetSection("Integrations:Smtp");
    private bool Enabled => !string.IsNullOrWhiteSpace(Smtp["Host"]) && !string.IsNullOrWhiteSpace(Smtp["From"]);

    private IDataProtector Protector(Invitation invitation) =>
        protection.CreateProtector("FlowOps.InvitationEmail.v1", invitation.OrganizationId.ToString(), invitation.Id.ToString());

    public void Enqueue(Invitation invitation, string code)
    {
        if (!Enabled) return;
        invitation.EmailCode = Protector(invitation).Protect(code);
        invitation.EmailNextAttemptAt = DateTimeOffset.UtcNow;
    }

    protected override async Task ExecuteAsync(CancellationToken token)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(2));
        while (await timer.WaitForNextTickAsync(token))
        {
            try
            {
                for (var count = 0; count < 10 && await DeliverAsync(token); count++) { }
            }
            catch (Exception exception) when (!token.IsCancellationRequested)
            {
                logger.LogWarning("Invitation email queue failed ({ErrorType})", exception.GetType().Name);
            }
        }
    }

    private async Task<bool> DeliverAsync(CancellationToken token)
    {
        await using var scope = scopes.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<FlowOpsDb>();
        await using var transaction = await db.Database.BeginTransactionAsync(token);
        var now = DateTimeOffset.UtcNow;
        var enabled = Enabled;
        var candidates = await db.Invitations.FromSqlInterpolated($"""
            SELECT * FROM flowops."Invitations"
            WHERE "EmailCode" IS NOT NULL AND ("ExpiresAt" <= {now} OR ({enabled} AND "EmailNextAttemptAt" <= {now}))
            ORDER BY "EmailNextAttemptAt", "Id" LIMIT 1 FOR UPDATE SKIP LOCKED
            """).ToListAsync(token);
        if (candidates.SingleOrDefault() is not { } invitation) return false;

        if (invitation.ExpiresAt <= now || invitation.RevokedAt is not null || invitation.AcceptedAt is not null)
            invitation.EmailCode = null;
        else
        {
            try
            {
                var origin = configuration["Auth:WebOrigin"] ?? "http://localhost:3000";
                var link = new Uri(new Uri(origin), "/join#" + Protector(invitation).Unprotect(invitation.EmailCode!));
                using var timeout = CancellationTokenSource.CreateLinkedTokenSource(token);
                timeout.CancelAfter(TimeSpan.FromSeconds(12));
                using var client = SmtpDelivery.CreateClient(Smtp);
                using var mail = new MailMessage(Smtp["From"]!, invitation.Email)
                {
                    Subject = "Your FlowOps invitation",
                    Body = $"You have been invited to join an organization in FlowOps. Sign in with this email address.\n\n{link.AbsoluteUri}"
                };
                await client.SendMailAsync(mail, timeout.Token);
                invitation.EmailCode = null;
            }
            catch (Exception exception) when (!token.IsCancellationRequested)
            {
                invitation.EmailAttempts++;
                invitation.EmailNextAttemptAt = now.AddSeconds(Math.Min(900, 1 << Math.Min(invitation.EmailAttempts, 10)));
                logger.LogWarning("Invitation {InvitationId} email attempt {Attempt} failed ({ErrorType})",
                    invitation.Id, invitation.EmailAttempts, exception.GetType().Name);
            }
        }
        await db.SaveChangesAsync(token);
        await transaction.CommitAsync(token);
        return true;
    }
}
