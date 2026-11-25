namespace FactoryPulse.Application.Monitoring;

public sealed record MachineTelemetry(
    Guid MachineId, DateTime TimestampUtc, double Temperature, double Vibration, int Rpm, double Torque, double ToolWear);
