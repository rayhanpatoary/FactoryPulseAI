using FactoryPulse.Application.Status;
using Xunit;

namespace FactoryPulse.UnitTests;

public sealed class ApplicationStatusServiceTests
{
    [Fact]
    public void StatusReportsConfiguredInstanceAndCurrentUtcTimeInSimulationMode()
    {
        var observedAt = new DateTimeOffset(2026, 1, 1, 8, 0, 0, TimeSpan.Zero);
        var clock = new TestClock { UtcNow = observedAt };
        var options = new ApplicationStatusOptions { InstanceName = "Assembly Demo" };
        var service = new ApplicationStatusService(options, clock);

        ApplicationStatus first = service.GetStatus();
        clock.UtcNow = observedAt.AddMinutes(1);
        ApplicationStatus second = service.GetStatus();

        Assert.Equal(new ApplicationStatus("Assembly Demo", "simulation", observedAt), first);
        Assert.Equal(first with { ObservedAtUtc = clock.UtcNow }, second);
    }

    private sealed class TestClock : TimeProvider
    {
        public DateTimeOffset UtcNow { get; set; }

        public override DateTimeOffset GetUtcNow() => UtcNow;
    }
}
