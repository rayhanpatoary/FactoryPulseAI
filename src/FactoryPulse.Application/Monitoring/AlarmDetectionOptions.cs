namespace FactoryPulse.Application.Monitoring;

public sealed class AlarmDetectionOptions
{
    public const string SectionName = "AlarmDetection";

    public bool Enabled { get; init; } = true;
    public double TemperatureWarning { get; init; } = 80;
    public double TemperatureWarningClear { get; init; } = 77;
    public double TemperatureCritical { get; init; } = 95;
    public double TemperatureCriticalClear { get; init; } = 90;
    public double VibrationWarning { get; init; } = 5;
    public double VibrationWarningClear { get; init; } = 4;

    public bool HasValidThresholds() =>
        double.IsFinite(TemperatureWarningClear)
        && TemperatureWarningClear < TemperatureWarning
        && TemperatureWarning <= TemperatureCriticalClear
        && TemperatureCriticalClear < TemperatureCritical
        && double.IsFinite(TemperatureCritical)
        && VibrationWarningClear >= 0
        && VibrationWarningClear < VibrationWarning
        && double.IsFinite(VibrationWarning);
}
