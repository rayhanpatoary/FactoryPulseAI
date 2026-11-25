namespace FactoryPulse.Domain.Monitoring;

public sealed class MachineReading
{
    private MachineReading() { }

    public MachineReading(Guid machineId, DateTime timestampUtc, double temperature, double vibration, int rpm, double torque, double toolWear)
    {
        if (machineId == Guid.Empty)
        {
            throw new ArgumentException("A reading requires a machine ID.", nameof(machineId));
        }

        MonitoringValidation.RequireUtc(timestampUtc);
        RequireFinite(temperature);
        RequireFinite(vibration);
        RequireFinite(torque);
        RequireFinite(toolWear);
        ArgumentOutOfRangeException.ThrowIfNegative(vibration);
        ArgumentOutOfRangeException.ThrowIfNegative(rpm);
        ArgumentOutOfRangeException.ThrowIfLessThan(toolWear, 0);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(toolWear, 1);

        MachineId = machineId;
        TimestampUtc = timestampUtc;
        Temperature = temperature;
        Vibration = vibration;
        Rpm = rpm;
        Torque = torque;
        ToolWear = toolWear;
    }

    public long Id { get; private set; }
    public Guid MachineId { get; private set; }
    public DateTime TimestampUtc { get; private set; }
    public double Temperature { get; private set; }
    public double Vibration { get; private set; }
    public int Rpm { get; private set; }
    public double Torque { get; private set; }
    public double ToolWear { get; private set; }

    private static void RequireFinite(double value, [System.Runtime.CompilerServices.CallerArgumentExpression(nameof(value))] string? parameter = null)
    {
        if (!double.IsFinite(value))
        {
            throw new ArgumentOutOfRangeException(parameter, "A measurement must be finite.");
        }
    }
}
