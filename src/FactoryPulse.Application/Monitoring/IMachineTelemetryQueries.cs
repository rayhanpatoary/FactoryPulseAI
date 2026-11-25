namespace FactoryPulse.Application.Monitoring;

public interface IMachineTelemetryQueries
{
    Task<MachineTelemetry?> GetLatestAsync(Guid machineId, CancellationToken cancellationToken);
    Task<TelemetryHistoryPage> GetHistoryAsync(Guid machineId, TelemetryHistoryFilter filter, CancellationToken cancellationToken);
}
