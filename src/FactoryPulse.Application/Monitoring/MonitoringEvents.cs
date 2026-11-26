using FactoryPulse.Domain.Monitoring;

namespace FactoryPulse.Application.Monitoring;

public abstract record MonitoringEvent;

public sealed record MachineReadingUpdated(
    Guid MachineId, string? MachineCode, DateTime TimestampUtc, double Temperature,
    double Vibration, int Rpm, double Torque, double ToolWear, MachineStatus? Status) : MonitoringEvent
{
    public static MachineReadingUpdated From(MachineReading reading, Machine? machine = null) =>
        new(reading.MachineId, machine?.MachineCode, reading.TimestampUtc, reading.Temperature,
            reading.Vibration, reading.Rpm, reading.Torque, reading.ToolWear, machine?.Status);
}

public sealed record AlarmOpened(
    long AlarmId, Guid MachineId, string AlarmCode, AlarmSeverity Severity, DateTime CreatedAtUtc,
    bool IsActive, bool IsAcknowledged) : MonitoringEvent
{
    public static AlarmOpened From(MachineAlarm alarm) =>
        new(alarm.Id, alarm.MachineId, alarm.AlarmCode, alarm.Severity, alarm.CreatedAtUtc,
            IsActive: true, IsAcknowledged: false);
}

public sealed record AlarmResolved(
    long AlarmId, Guid MachineId, string AlarmCode, DateTime ResolvedAtUtc) : MonitoringEvent
{
    public static AlarmResolved From(MachineAlarm alarm, DateTime resolvedAtUtc) =>
        new(alarm.Id, alarm.MachineId, alarm.AlarmCode, resolvedAtUtc);
}

public sealed record AlarmAcknowledged(
    long AlarmId, Guid MachineId, DateTime AcknowledgedAtUtc, bool IsAcknowledged) : MonitoringEvent;
