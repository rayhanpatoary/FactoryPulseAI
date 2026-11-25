using FactoryPulse.Application.Monitoring;
using FactoryPulse.Domain.Monitoring;
using Xunit;

namespace FactoryPulse.UnitTests;

public sealed class AlarmDetectionTests
{
    private static readonly Guid MachineId = new("9cf16c97-f776-48f0-bb78-e508d447438a");
    private readonly AlarmDetectionService detector = new(new AlarmDetectionOptions());

    [Theory]
    [InlineData(79.9, 4.9, null, false)]
    [InlineData(80, 0, AlarmSeverity.Warning, false)]
    [InlineData(95, 0, AlarmSeverity.Critical, false)]
    [InlineData(25, 5, null, true)]
    public void TriggerThresholdsAreInclusive(double temperature, double vibration, AlarmSeverity? severity, bool excessiveVibration)
    {
        Assert.Equal(new AlarmConditions(severity, excessiveVibration), detector.Evaluate(Reading(temperature, vibration), default));
    }

    [Fact]
    public void ExistingConditionsUseClearThresholdsAndCanChangeSeverity()
    {
        AlarmConditions critical = detector.Evaluate(Reading(95, 5), default);
        Assert.Equal(critical, detector.Evaluate(Reading(90, 4), critical));

        AlarmConditions warning = detector.Evaluate(Reading(89, 3.9), critical);
        Assert.Equal(new AlarmConditions(AlarmSeverity.Warning, false), warning);
        Assert.Equal(warning, detector.Evaluate(Reading(77, 0), warning));
        Assert.Equal(default, detector.Evaluate(Reading(76.9, 0), warning));
    }

    [Fact]
    public void DisabledDetectionLeavesExistingStateIntact()
    {
        var disabled = new AlarmDetectionService(new AlarmDetectionOptions { Enabled = false });
        var active = new AlarmConditions(AlarmSeverity.Critical, true);
        Assert.Equal(active, disabled.Evaluate(Reading(25, 0), active));
    }

    [Fact]
    public void ThresholdValidationRejectsUnorderedAndNonfiniteThresholds()
    {
        Assert.True(new AlarmDetectionOptions().HasValidThresholds());
        Assert.False(new AlarmDetectionOptions { TemperatureWarningClear = 80 }.HasValidThresholds());
        Assert.False(new AlarmDetectionOptions { TemperatureCriticalClear = 79 }.HasValidThresholds());
        Assert.False(new AlarmDetectionOptions { VibrationWarning = double.PositiveInfinity }.HasValidThresholds());
        Assert.False(new AlarmDetectionOptions { VibrationWarningClear = -1 }.HasValidThresholds());
    }

    private static MachineReading Reading(double temperature, double vibration) =>
        new(MachineId, DateTime.UnixEpoch, temperature, vibration, 2300, 20, 0.1);
}
