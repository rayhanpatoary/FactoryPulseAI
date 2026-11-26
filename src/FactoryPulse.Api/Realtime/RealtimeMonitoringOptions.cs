namespace FactoryPulse.Api.Realtime;

internal sealed class RealtimeMonitoringOptions
{
    public const string SectionName = "RealtimeMonitoring";

    public bool Enabled { get; init; } = true;
    public bool AllowProduction { get; init; }

    public bool IsAvailable(IHostEnvironment environment) =>
        Enabled && (environment.IsDevelopment() || environment.IsEnvironment("Testing")
            || (environment.IsProduction() && AllowProduction));
}
