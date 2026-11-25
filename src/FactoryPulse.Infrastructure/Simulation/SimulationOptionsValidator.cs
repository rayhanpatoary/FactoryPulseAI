using FactoryPulse.Domain.Monitoring;
using Microsoft.Extensions.Options;

namespace FactoryPulse.Infrastructure.Simulation;

public sealed class SimulationOptionsValidator : IValidateOptions<SimulationOptions>
{
    public ValidateOptionsResult Validate(string? name, SimulationOptions options)
    {
        if (options.SamplingInterval < TimeSpan.FromMilliseconds(100) || options.SamplingInterval > TimeSpan.FromMinutes(1)
            || options.PersistenceInterval < options.SamplingInterval || options.PersistenceInterval > TimeSpan.FromHours(1))
        {
            return ValidateOptionsResult.Fail("Simulation sampling must be 100 ms to 1 minute and persistence must be between the sampling interval and 1 hour.");
        }

        TimeSpan maximumTimeConstant = TimeSpan.FromHours(1);
        if (options.HeatingTimeConstant <= TimeSpan.Zero || options.HeatingTimeConstant > maximumTimeConstant
            || options.CoolingTimeConstant <= TimeSpan.Zero || options.CoolingTimeConstant > maximumTimeConstant)
        {
            return ValidateOptionsResult.Fail("Simulation heating and cooling time constants must be positive and at most 1 hour.");
        }

        if (options.Profiles is not { Length: >= 1 and <= 32 })
        {
            return ValidateOptionsResult.Fail("Simulation must define between 1 and 32 machine profiles.");
        }

        var ids = new HashSet<Guid>();
        var codes = new HashSet<string>(StringComparer.Ordinal);
        for (int index = 0; index < options.Profiles.Length; index++)
        {
            SimulationProfile profile = options.Profiles[index];
            if (profile is null || profile.Id == Guid.Empty || !ids.Add(profile.Id)
                || profile.Code is not { Length: >= 1 and <= Machine.CodeMaxLength } code
                || !code.All(character => char.IsAsciiLetterUpper(character) || char.IsAsciiDigit(character) || character is '-' or '_')
                || !codes.Add(code))
            {
                return ValidateOptionsResult.Fail($"Simulation:Profiles:{index} requires a unique nonempty ID and canonical machine code.");
            }

            if (profile.Status is not (MachineStatus.Running or MachineStatus.Idle or MachineStatus.Offline)
                || profile.TargetRpm is < 1 or > 40_000
                || profile.AmbientTemperature is not (>= -50 and <= 80)
                || !(profile.OperatingTemperature >= profile.AmbientTemperature && profile.OperatingTemperature <= 200)
                || profile.RunningVibration is not (> 0 and <= 50)
                || profile.RatedTorque is not (> 0 and <= 5_000)
                || profile.ToolWearPerHour is not (>= 0 and <= 1))
            {
                return ValidateOptionsResult.Fail($"Simulation:Profiles:{index} has an unsupported operating state or measurement setting.");
            }
        }

        return ValidateOptionsResult.Success;
    }
}
