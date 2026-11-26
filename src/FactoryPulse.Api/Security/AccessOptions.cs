using System.ComponentModel.DataAnnotations;

namespace FactoryPulse.Api.Security;

internal sealed class AccessOptions
{
    [Range(5, 120)]
    public int SessionMinutes { get; init; } = 30;
}
