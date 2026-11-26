using FactoryPulse.Api.Security;
using FactoryPulse.Infrastructure.Identity;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.SignalR;

namespace FactoryPulse.Api.Realtime;

public sealed class MonitoringHub(ActiveMonitoringSessions sessions, SignInManager<MonitoringUser> signIn) : Hub
{
    public override async Task OnConnectedAsync()
    {
        if (Context.UserIdentifier is not string userId)
        {
            Context.Abort();
            return;
        }

        sessions.Add(userId, Context.ConnectionId, Context.Abort);
        // Catch a logout racing the original handshake before the connection was registered.
        if (Context.User?.Identity?.AuthenticationType == IdentityConstants.ApplicationScheme
            && await signIn.ValidateSecurityStampAsync(Context.User) is not { IsActive: true })
        {
            Context.Abort();
        }
    }

    public override Task OnDisconnectedAsync(Exception? exception)
    {
        if (Context.UserIdentifier is string userId)
        {
            sessions.Remove(userId, Context.ConnectionId);
        }

        return base.OnDisconnectedAsync(exception);
    }
}
