namespace FactoryPulse.Application.Status;

public sealed record ApplicationStatus(string InstanceName, string OperatingMode, DateTimeOffset ObservedAtUtc);
