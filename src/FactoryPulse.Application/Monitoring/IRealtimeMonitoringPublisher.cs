namespace FactoryPulse.Application.Monitoring;

public interface IRealtimeMonitoringPublisher
{
    bool IsEnabled { get; }

    Task PublishAsync(IReadOnlyList<MonitoringEvent> events, CancellationToken cancellationToken);
}
