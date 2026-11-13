using System.Net;
using System.Net.Mail;

namespace FlowOps.Api;

internal static class SmtpDelivery
{
    public static SmtpClient CreateClient(IConfigurationSection configuration)
    {
        var client = new SmtpClient(configuration["Host"]!, configuration.GetValue("Port", 587))
        {
            EnableSsl = configuration.GetValue("Tls", true)
        };
        if (configuration["User"] is { Length: > 0 } user)
            client.Credentials = new NetworkCredential(user, configuration["Password"]);
        return client;
    }
}
