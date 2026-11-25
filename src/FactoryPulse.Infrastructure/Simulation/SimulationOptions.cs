namespace FactoryPulse.Infrastructure.Simulation;

public sealed class SimulationOptions
{
    public const string SectionName = "Simulation";

    public bool Enabled { get; init; }
    public int Seed { get; init; } = 42;
    public TimeSpan SamplingInterval { get; init; } = TimeSpan.FromSeconds(1);
    public TimeSpan PersistenceInterval { get; init; } = TimeSpan.FromSeconds(5);
    public TimeSpan HeatingTimeConstant { get; init; } = TimeSpan.FromSeconds(30);
    public TimeSpan CoolingTimeConstant { get; init; } = TimeSpan.FromSeconds(45);
    public SimulationProfile[] Profiles { get; init; } = [];
}
