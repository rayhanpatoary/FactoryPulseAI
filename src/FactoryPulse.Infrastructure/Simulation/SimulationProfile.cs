using FactoryPulse.Domain.Monitoring;

namespace FactoryPulse.Infrastructure.Simulation;

public sealed class SimulationProfile
{
    public Guid Id { get; init; }
    public string Code { get; init; } = string.Empty;
    public MachineStatus Status { get; init; }
    public int TargetRpm { get; init; }
    public double AmbientTemperature { get; init; }
    public double OperatingTemperature { get; init; }
    public double RunningVibration { get; init; }
    public double RatedTorque { get; init; }
    public double ToolWearPerHour { get; init; }
}
