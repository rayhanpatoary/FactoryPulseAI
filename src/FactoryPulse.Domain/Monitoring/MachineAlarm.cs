namespace FactoryPulse.Domain.Monitoring;

public sealed class MachineAlarm
{
    public const int CodeMaxLength = 50;
    public const int MessageMaxLength = 500;

    private MachineAlarm() { }

    public MachineAlarm(Guid machineId, string alarmCode, string message, AlarmSeverity severity, DateTime createdAtUtc)
    {
        if (machineId == Guid.Empty)
        {
            throw new ArgumentException("An alarm requires a machine ID.", nameof(machineId));
        }

        if (!Enum.IsDefined(severity))
        {
            throw new ArgumentOutOfRangeException(nameof(severity));
        }

        MonitoringValidation.RequireUtc(createdAtUtc);
        MachineId = machineId;
        AlarmCode = MonitoringValidation.RequiredText(alarmCode, CodeMaxLength);
        Message = MonitoringValidation.RequiredText(message, MessageMaxLength);
        Severity = severity;
        CreatedAtUtc = createdAtUtc;
    }

    public long Id { get; private set; }
    public Guid MachineId { get; private set; }
    public string AlarmCode { get; private set; } = null!;
    public string Message { get; private set; } = null!;
    public AlarmSeverity Severity { get; private set; }
    public DateTime CreatedAtUtc { get; private set; }
    public DateTime? AcknowledgedAtUtc { get; private set; }
    public DateTime? ResolvedAtUtc { get; private set; }
    public bool IsAcknowledged => AcknowledgedAtUtc.HasValue;
    public bool IsActive => !ResolvedAtUtc.HasValue;

    public void Acknowledge(DateTime acknowledgedAtUtc)
    {
        MonitoringValidation.RequireUtc(acknowledgedAtUtc);
        ArgumentOutOfRangeException.ThrowIfLessThan(acknowledgedAtUtc, CreatedAtUtc);
        AcknowledgedAtUtc ??= acknowledgedAtUtc;
    }

    public void Resolve(DateTime resolvedAtUtc)
    {
        MonitoringValidation.RequireUtc(resolvedAtUtc);
        ArgumentOutOfRangeException.ThrowIfLessThan(resolvedAtUtc, CreatedAtUtc);
        ResolvedAtUtc ??= resolvedAtUtc;
    }
}
