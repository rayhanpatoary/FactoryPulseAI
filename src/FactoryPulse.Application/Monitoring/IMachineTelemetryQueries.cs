namespace FactoryPulse.Application.Monitoring;

public interface IMachineTelemetryQueries
{
    Task<MachineTelemetry?> GetLatestAsync(Guid machineId, CancellationToken cancellationToken);
}
