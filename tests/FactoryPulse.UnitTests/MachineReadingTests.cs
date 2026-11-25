using FactoryPulse.Domain.Monitoring;
using Xunit;

namespace FactoryPulse.UnitTests;

public sealed class MachineReadingTests
{
    private static readonly Guid MachineId = new("93d2c76f-f0db-4460-afce-f696ea0a3825");
    private static readonly DateTime TimestampUtc = new(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void ReadingRequiresAUtcTimestamp()
    {
        DateTime unspecified = DateTime.SpecifyKind(TimestampUtc, DateTimeKind.Unspecified);

        Assert.Throws<ArgumentException>(() => new MachineReading(MachineId, unspecified, 40, 0.1, 500, 8, 0.2));
    }

    [Theory]
    [InlineData(-0.1)]
    [InlineData(1.1)]
    [InlineData(double.NaN)]
    public void ReadingRejectsToolWearOutsideTheUnitInterval(double toolWear)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new MachineReading(MachineId, TimestampUtc, 40, 0.1, 500, 8, toolWear));
    }
}
