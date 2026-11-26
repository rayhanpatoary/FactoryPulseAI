using FactoryPulse.Api.Security;
using Microsoft.Extensions.Options;

namespace FactoryPulse.Api.Realtime;

internal static class MonitoringEndpoint
{
    private const string Path = "/hubs/monitoring";

    internal static void MapFactoryPulseMonitoringHub(this WebApplication app)
    {
        if (!app.Services.GetRequiredService<IOptions<RealtimeMonitoringOptions>>().Value.IsAvailable(app.Environment))
        {
            return;
        }

        // Apply the same-origin policy to negotiation and all transports, including browser WebSockets.
        app.Use(async (context, next) =>
        {
            HttpRequest request = context.Request;
            if (request.Path.StartsWithSegments(Path)
                && request.Headers.TryGetValue("Origin", out var origins)
                && (origins.Count != 1 || !Uri.TryCreate($"{request.Scheme}://{request.Host}", UriKind.Absolute, out Uri? expected)
                    || !string.Equals(origins[0], expected.GetLeftPart(UriPartial.Authority), StringComparison.OrdinalIgnoreCase)))
            {
                context.Response.StatusCode = StatusCodes.Status403Forbidden;
                return;
            }

            await next(context);
        });

        app.MapHub<MonitoringHub>(Path, options => options.CloseOnAuthenticationExpiration = true)
            .RequireAuthorization(MonitoringAccess.Read);
    }
}
