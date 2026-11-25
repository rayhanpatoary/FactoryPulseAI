using FactoryPulse.Application.Monitoring;
using FactoryPulse.Domain.Monitoring;
using Microsoft.Extensions.Options;

namespace FactoryPulse.Infrastructure.Simulation;

public sealed class SimulatedMachineDataSource : IMachineDataSource
{
    private readonly Dictionary<string, MachineState> states;
    private readonly TimeProvider clock;
    private readonly double heatingResponse;
    private readonly double coolingResponse;
    private readonly double sampleHours;

    public SimulatedMachineDataSource(IOptions<SimulationOptions> options, TimeProvider clock)
    {
        SimulationOptions settings = options.Value;
        this.clock = clock;
        states = settings.Profiles.ToDictionary(profile => profile.Code,
            profile => new MachineState(profile, MakeSeed(settings.Seed, profile.Id)), StringComparer.Ordinal);
        heatingResponse = 1 - Math.Exp(-settings.SamplingInterval / settings.HeatingTimeConstant);
        coolingResponse = 1 - Math.Exp(-settings.SamplingInterval / settings.CoolingTimeConstant);
        sampleHours = settings.SamplingInterval.TotalHours;
    }

    public ValueTask<MachineReading> ReadAsync(Machine machine, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (!states.TryGetValue(machine.MachineCode, out MachineState? state))
        {
            throw new ArgumentException("The machine has no configured simulation profile.", nameof(machine));
        }

        SimulationProfile profile = state.Profile;
        DateTime now = clock.GetUtcNow().UtcDateTime;
        if (machine.Status != MachineStatus.Running)
        {
            state.Temperature += (profile.AmbientTemperature - state.Temperature) * coolingResponse;
            return ValueTask.FromResult(new MachineReading(machine.Id, now, state.Temperature, 0, 0, 0, state.Wear));
        }

        // Smooth the load so consecutive readings stay correlated.
        state.Load += (state.Random.NextDouble() - state.Load) * 0.2;
        state.Temperature += (profile.OperatingTemperature - state.Temperature) * heatingResponse;
        state.Wear = Math.Min(1, state.Wear + profile.ToolWearPerHour * sampleHours * (0.5 + 0.5 * state.Load));
        int rpm = (int)Math.Round(profile.TargetRpm * (0.96 + 0.08 * state.Load));
        double vibration = profile.RunningVibration * (0.8 + 0.4 * state.Load);
        double torque = profile.RatedTorque * (0.55 + 0.4 * state.Load);

        return ValueTask.FromResult(new MachineReading(machine.Id, now, state.Temperature, vibration, rpm, torque, state.Wear));
    }

    private static int MakeSeed(int seed, Guid id)
    {
        foreach (byte value in id.ToByteArray())
        {
            seed = unchecked(seed * 31 + value);
        }

        return seed;
    }

    private sealed class MachineState(SimulationProfile profile, int seed)
    {
        internal SimulationProfile Profile { get; } = profile;
        internal Random Random { get; } = new(seed);
        internal double Load { get; set; } = 0.5;
        internal double Temperature { get; set; } = profile.AmbientTemperature;
        internal double Wear { get; set; }
    }
}
