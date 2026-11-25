using FactoryPulse.Domain.Monitoring;

namespace FactoryPulse.Application.Monitoring;

public readonly record struct AlarmConditions(AlarmSeverity? Temperature, bool Vibration);

public sealed class AlarmDetectionService(AlarmDetectionOptions settings)
{
    public bool Enabled => settings.Enabled;

    public AlarmConditions Evaluate(MachineReading reading, AlarmConditions current)
    {
        if (!Enabled)
        {
            return current;
        }

        double temperature = reading.Temperature;
        AlarmSeverity? nextTemperature = null;
        if (temperature >= settings.TemperatureCritical ||
            (current.Temperature == AlarmSeverity.Critical && temperature >= settings.TemperatureCriticalClear))
        {
            nextTemperature = AlarmSeverity.Critical;
        }
        else if (temperature >= settings.TemperatureWarning ||
            (current.Temperature.HasValue && temperature >= settings.TemperatureWarningClear))
        {
            nextTemperature = AlarmSeverity.Warning;
        }

        double vibrationLimit = current.Vibration ? settings.VibrationWarningClear : settings.VibrationWarning;
        return new AlarmConditions(nextTemperature, reading.Vibration >= vibrationLimit);
    }
}
