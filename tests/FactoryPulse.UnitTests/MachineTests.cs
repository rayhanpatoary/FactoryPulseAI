using FactoryPulse.Domain.Monitoring;
using Xunit;

namespace FactoryPulse.UnitTests;

public sealed class MachineTests
{
    private static readonly DateTime CreatedAtUtc = new(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void ConstructorNormalizesCodeAndStatusChangesAdvanceTheUpdateTime()
    {
        var machine = new Machine(" cnc-01 ", "CNC", "Mill", "Hall A", CreatedAtUtc);
        Assert.Equal("CNC-01", machine.MachineCode);
        Assert.Equal(MachineStatus.Offline, machine.Status);

        machine.ChangeStatus(MachineStatus.Running, CreatedAtUtc.AddMinutes(1));

        Assert.Equal(MachineStatus.Running, machine.Status);
        Assert.Equal(CreatedAtUtc.AddMinutes(1), machine.UpdatedAtUtc);
        Assert.Throws<ArgumentOutOfRangeException>(() => machine.ChangeStatus(MachineStatus.Idle, CreatedAtUtc));
    }

    [Theory]
    [InlineData("")]
    [InlineData("cnc 01")]
    public void ConstructorRejectsInvalidCodes(string code)
    {
        Assert.Throws<ArgumentException>(() => new Machine(code, "CNC", "Mill", "Hall A", CreatedAtUtc));
    }
}
