using System.Security.Claims;
using Microsoft.AspNetCore.SignalR;

namespace FlowOps.Api;

internal sealed class NotificationHub : Hub;

internal sealed class KeycloakUserIdProvider : IUserIdProvider
{
    public string? GetUserId(HubConnectionContext connection) => connection.User.FindFirstValue("sub");
}
