using Microsoft.AspNetCore.Identity;

namespace FactoryPulse.Infrastructure.Identity;

public sealed class MonitoringUser : IdentityUser
{
    public bool IsActive { get; set; } = true;
}
