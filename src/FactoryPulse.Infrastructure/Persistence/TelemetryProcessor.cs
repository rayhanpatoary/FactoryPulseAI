using FactoryPulse.Application.Monitoring;
using FactoryPulse.Domain.Monitoring;
using Microsoft.Extensions.DependencyInjection;

namespace FactoryPulse.Infrastructure.Persistence;

// One instance belongs to one sequential telemetry ingestion loop.
public sealed class TelemetryProcessor(
    AlarmDetectionService detector, IServiceScopeFactory scopes, IRealtimeMonitoringPublisher realtime)
{
    private static readonly AlarmType WarningType = new(AlarmCodes.TemperatureWarning, AlarmSeverity.Warning, "High machine temperature.");
    private static readonly AlarmType CriticalType = new(AlarmCodes.TemperatureCritical, AlarmSeverity.Critical, "Critical machine temperature.");
    private static readonly AlarmType VibrationType = new(AlarmCodes.VibrationWarning, AlarmSeverity.Warning, "Excessive machine vibration.");

    private readonly Dictionary<Guid, ActiveAlarms> active = [];
    private readonly Dictionary<Guid, ActiveAlarms> pending = [];
    private readonly List<MachineAlarm> opened = [];
    private readonly List<AlarmResolution> resolutions = [];
    private readonly List<(MachineAlarm Alarm, DateTime? ResolvedAtUtc)> transitions = [];

    public async Task InitializeAsync(Guid[] machineIds, CancellationToken cancellationToken)
    {
        active.Clear();
        if (!detector.Enabled)
        {
            return;
        }

        foreach (Guid id in machineIds)
        {
            active.Add(id, default);
        }

        await using AsyncServiceScope scope = scopes.CreateAsyncScope();
        List<MachineAlarm> alarms = await scope.ServiceProvider.GetRequiredService<TelemetryStore>()
            .LoadActiveAsync(machineIds, cancellationToken);
        foreach (MachineAlarm alarm in alarms)
        {
            ActiveAlarms state = active[alarm.MachineId];
            state = alarm.AlarmCode switch
            {
                AlarmCodes.TemperatureWarning => state with { Warning = alarm },
                AlarmCodes.TemperatureCritical => state with { Critical = alarm },
                _ => state with { Vibration = alarm }
            };
            if (state.ObservedAtUtc is null || state.ObservedAtUtc < alarm.CreatedAtUtc)
            {
                state = state with { ObservedAtUtc = alarm.CreatedAtUtc };
            }

            active[alarm.MachineId] = state;
        }
    }

    public async Task<bool> ProcessAsync(
        IReadOnlyList<MachineReading> readings, bool persistenceDue, CancellationToken cancellationToken)
    {
        pending.Clear();
        opened.Clear();
        resolutions.Clear();
        transitions.Clear();
        if (detector.Enabled)
        {
            foreach (MachineReading reading in readings)
            {
                cancellationToken.ThrowIfCancellationRequested();
                ActiveAlarms current = pending.GetValueOrDefault(reading.MachineId, active[reading.MachineId]);
                if (reading.TimestampUtc < current.ObservedAtUtc)
                {
                    continue;
                }

                AlarmConditions next = detector.Evaluate(reading, current.Conditions);
                pending[reading.MachineId] = new ActiveAlarms(
                    Reconcile(current.Warning, next.Temperature == AlarmSeverity.Warning, reading, WarningType),
                    Reconcile(current.Critical, next.Temperature == AlarmSeverity.Critical, reading, CriticalType),
                    Reconcile(current.Vibration, next.Vibration, reading, VibrationType),
                    reading.TimestampUtc);
            }
        }

        bool persisted = persistenceDue || opened.Count > 0 || resolutions.Count > 0;
        if (persisted)
        {
            await using AsyncServiceScope scope = scopes.CreateAsyncScope();
            await scope.ServiceProvider.GetRequiredService<TelemetryStore>()
                .SaveBatchAsync(readings, opened, resolutions, cancellationToken);
        }

        // Publish cached transitions only after the database write succeeds.
        foreach ((Guid id, ActiveAlarms state) in pending)
        {
            active[id] = state;
        }

        if (transitions.Count > 0)
        {
            // The successful batch assigned alarm IDs and committed all lifecycle changes.
            var events = new MonitoringEvent[transitions.Count];
            for (int index = 0; index < events.Length; index++)
            {
                (MachineAlarm alarm, DateTime? resolvedAt) = transitions[index];
                events[index] = resolvedAt is DateTime timestamp ? AlarmResolved.From(alarm, timestamp) : AlarmOpened.From(alarm);
            }

            await realtime.PublishAsync(events, cancellationToken);
        }

        return persisted;
    }

    private MachineAlarm? Reconcile(MachineAlarm? activeAlarm, bool wanted, MachineReading reading, AlarmType type)
    {
        if (wanted)
        {
            if (activeAlarm is null)
            {
                activeAlarm = new MachineAlarm(reading.MachineId, type.Code, type.Message, type.Severity, reading.TimestampUtc);
                opened.Add(activeAlarm);
                RecordTransition(activeAlarm, resolvedAtUtc: null);
            }

            return activeAlarm;
        }

        if (activeAlarm is { Id: 0 })
        {
            activeAlarm.Resolve(reading.TimestampUtc);
        }
        else if (activeAlarm is not null)
        {
            resolutions.Add(new AlarmResolution(activeAlarm.Id, reading.TimestampUtc));
        }

        if (activeAlarm is not null)
        {
            RecordTransition(activeAlarm, reading.TimestampUtc);
        }

        return null;
    }

    private void RecordTransition(MachineAlarm alarm, DateTime? resolvedAtUtc)
    {
        if (realtime.IsEnabled)
        {
            transitions.Add((alarm, resolvedAtUtc));
        }
    }

    private readonly record struct AlarmType(string Code, AlarmSeverity Severity, string Message);

    private readonly record struct ActiveAlarms(
        MachineAlarm? Warning, MachineAlarm? Critical, MachineAlarm? Vibration, DateTime? ObservedAtUtc)
    {
        internal AlarmConditions Conditions => new(Critical?.Severity ?? Warning?.Severity, Vibration is not null);
    }
}
