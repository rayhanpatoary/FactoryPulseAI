namespace FactoryPulse.Application.Status;

public sealed class ApplicationStatusService(ApplicationStatusOptions options, TimeProvider timeProvider)
{
    public ApplicationStatus GetStatus()
    {
        return new ApplicationStatus(options.InstanceName, "simulation", timeProvider.GetUtcNow());
    }
}
