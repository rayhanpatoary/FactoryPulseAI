namespace FactoryPulse.Api.Security;

public static class MonitoringAccess
{
    public const string Viewer = "Viewer";
    public const string Operator = "Operator";
    public const string Administrator = "Administrator";
    public const string Read = "Monitoring.Read";
    public const string Acknowledge = "Monitoring.Acknowledge";
    public const string Administer = "Monitoring.Administer";

    public static void AddMonitoringPolicies(this IServiceCollection services)
    {
        services.AddAuthorizationBuilder()
            .AddPolicy(Read, policy => policy.RequireAuthenticatedUser().RequireRole(Viewer, Operator, Administrator))
            .AddPolicy(Acknowledge, policy => policy.RequireAuthenticatedUser().RequireRole(Operator, Administrator))
            .AddPolicy(Administer, policy => policy.RequireAuthenticatedUser().RequireRole(Administrator));
    }
}
