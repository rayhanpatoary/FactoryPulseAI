using FactoryPulse.Application.Monitoring;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Options;

namespace FactoryPulse.Api.Realtime;

internal sealed partial class SignalRMonitoringPublisher(
    IHubContext<MonitoringHub> hub, IOptions<RealtimeMonitoringOptions> options,
    IHostEnvironment environment, TimeProvider clock, ILogger<SignalRMonitoringPublisher> logger)
    : IRealtimeMonitoringPublisher
{
    private static readonly TimeSpan SendBudget = TimeSpan.FromMilliseconds(250);
    private static readonly TimeSpan WarningInterval = TimeSpan.FromMinutes(1);
    private readonly Lock warningLock = new();
    private long? lastWarning;

    public bool IsEnabled { get; } = options.Value.IsAvailable(environment);

    public async Task PublishAsync(IReadOnlyList<MonitoringEvent> events, CancellationToken cancellationToken)
    {
        if (!IsEnabled || events.Count == 0)
        {
            return;
        }

        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        deadline.CancelAfter(SendBudget);
        try
        {
            foreach (MonitoringEvent message in events)
            {
                deadline.Token.ThrowIfCancellationRequested();
                await hub.Clients.All.SendCoreAsync(EventName(message), [message], deadline.Token);
            }

            deadline.Token.ThrowIfCancellationRequested();
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            WarnAboutDeliveryFailure(exception);
        }
    }

    private static string EventName(MonitoringEvent message) => message switch
    {
        MachineReadingUpdated => nameof(MachineReadingUpdated),
        AlarmOpened => nameof(AlarmOpened),
        AlarmResolved => nameof(AlarmResolved),
        AlarmAcknowledged => nameof(AlarmAcknowledged),
        _ => throw new ArgumentOutOfRangeException(nameof(message), "Unsupported real-time monitoring event.")
    };

    private void WarnAboutDeliveryFailure(Exception exception)
    {
        lock (warningLock)
        {
            long now = clock.GetTimestamp();
            if (lastWarning is long previous && clock.GetElapsedTime(previous, now) < WarningInterval)
            {
                return;
            }

            lastWarning = now;
        }

        LogDeliveryFailure(logger, exception);
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "Real-time monitoring delivery was interrupted. Clients can refresh state through the REST API.")]
    private static partial void LogDeliveryFailure(ILogger logger, Exception exception);
}
