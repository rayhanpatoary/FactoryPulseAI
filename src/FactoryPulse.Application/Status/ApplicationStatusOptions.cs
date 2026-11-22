namespace FactoryPulse.Application.Status;

public sealed class ApplicationStatusOptions
{
    public const string SectionName = "ApplicationStatus";

    public string InstanceName { get; init; } = string.Empty;
}
