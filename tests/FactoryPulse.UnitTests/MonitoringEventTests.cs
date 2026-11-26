using FactoryPulse.Application.Monitoring;
using FactoryPulse.Domain.Monitoring;
using Xunit;

namespace FactoryPulse.UnitTests;

public sealed class MonitoringEventTests
{
    private static readonly DateTime ObservedAt = DateTime.UnixEpoch.AddSeconds(10);

    [Fact]
    public void ReadingCarriesMeasurementsAndKnownMachineMetadata()
    {
        var machine = new Machine("CNC-001", "Mill", "CNC", "Test", DateTime.UnixEpoch);
        machine.ChangeStatus(MachineStatus.Running, DateTime.UnixEpoch);
        var reading = new MachineReading(machine.Id, ObservedAt, 46, 1.2, 2400, 18, 0.1);

        MachineReadingUpdated message = MachineReadingUpdated.From(reading, machine);

        Assert.Equal(new MachineReadingUpdated(machine.Id, "CNC-001", ObservedAt, 46, 1.2, 2400, 18, 0.1, MachineStatus.Running), message);
        Assert.Equal(DateTimeKind.Utc, message.TimestampUtc.Kind);
        Assert.Equal(message with { MachineCode = null, Status = null }, MachineReadingUpdated.From(reading));
    }

    [Fact]
    public void AlarmMessagesCarryTheCodeAndTheTimestampOfEachTransition()
    {
        var alarm = new MachineAlarm(Guid.NewGuid(), AlarmCodes.TemperatureCritical, "High temperature", AlarmSeverity.Critical, ObservedAt);
        DateTime resolvedAt = ObservedAt.AddSeconds(2);
        alarm.Resolve(resolvedAt);

        AlarmOpened opened = AlarmOpened.From(alarm);
        AlarmResolved resolved = AlarmResolved.From(alarm, resolvedAt);

        Assert.Equal("TEMPERATURE_CRITICAL", opened.AlarmCode);
        Assert.Equal(AlarmSeverity.Critical, opened.Severity);
        Assert.Equal(ObservedAt, opened.CreatedAtUtc);
        Assert.Equal(DateTimeKind.Utc, opened.CreatedAtUtc.Kind);
        Assert.True(opened.IsActive);
        Assert.False(opened.IsAcknowledged);
        Assert.Equal(new AlarmResolved(opened.AlarmId, alarm.MachineId, opened.AlarmCode, resolvedAt), resolved);
        Assert.Equal(DateTimeKind.Utc, resolved.ResolvedAtUtc.Kind);
    }
}
