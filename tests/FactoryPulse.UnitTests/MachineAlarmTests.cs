using FactoryPulse.Domain.Monitoring;
using Xunit;

namespace FactoryPulse.UnitTests;

public sealed class MachineAlarmTests
{
    [Fact]
    public void AcknowledgementAndResolutionPreserveIndependentTimestamps()
    {
        var alarm = new MachineAlarm(MachineId, "OVERHEAT", "TEMP_HIGH", AlarmSeverity.Warning, CreatedAtUtc);
        alarm.Acknowledge(CreatedAtUtc.AddMinutes(1));
        Assert.True(alarm.IsAcknowledged);
        Assert.True(alarm.IsActive);
        Assert.Throws<ArgumentOutOfRangeException>(() => alarm.Resolve(CreatedAtUtc.AddTicks(-1)));

        alarm.Resolve(CreatedAtUtc.AddMinutes(2));
        alarm.Resolve(CreatedAtUtc.AddMinutes(3));
        Assert.False(alarm.IsActive);
        Assert.Equal(CreatedAtUtc.AddMinutes(1), alarm.AcknowledgedAtUtc);
        Assert.Equal(CreatedAtUtc.AddMinutes(2), alarm.ResolvedAtUtc);
    }

    private static readonly Guid MachineId = new("93d2c76f-f0db-4460-afce-f696ea0a3825");
    private static readonly DateTime CreatedAtUtc = new(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void AcknowledgementPreservesTheFirstTimestampOnRetry()
    {
        var alarm = new MachineAlarm(MachineId, "OVERHEAT", "TEMP_HIGH", AlarmSeverity.Warning, CreatedAtUtc);
        Assert.False(alarm.IsAcknowledged);

        alarm.Acknowledge(CreatedAtUtc.AddMinutes(1));
        alarm.Acknowledge(CreatedAtUtc.AddMinutes(2));

        Assert.True(alarm.IsAcknowledged);
        Assert.Equal(CreatedAtUtc.AddMinutes(1), alarm.AcknowledgedAtUtc);
    }

    [Fact]
    public void AcknowledgementCannotPredateTheAlarm()
    {
        var alarm = new MachineAlarm(MachineId, "OVERHEAT", "TEMP_HIGH", AlarmSeverity.Warning, CreatedAtUtc);

        Assert.Throws<ArgumentOutOfRangeException>(() => alarm.Acknowledge(CreatedAtUtc.AddTicks(-1)));
        Assert.False(alarm.IsAcknowledged);
    }
}
