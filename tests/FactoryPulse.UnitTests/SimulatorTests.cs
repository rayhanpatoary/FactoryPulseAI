using FactoryPulse.Domain.Monitoring;
using FactoryPulse.Infrastructure.Simulation;
using Microsoft.Extensions.Options;
using Xunit;

namespace FactoryPulse.UnitTests;

public sealed class SimulatorTests
{
    private static readonly DateTimeOffset Epoch = DateTimeOffset.UnixEpoch;
    private static readonly SimulationProfile LowSpeed = Profile("TEST-01", 1_200);
    private static readonly SimulationProfile HighSpeed = Profile("TEST-02", 3_500);

    [Fact]
    public async Task RunningMachineWarmsAndWearsWhileMeasurementsFollowItsProfile()
    {
        CancellationToken token = TestContext.Current.CancellationToken;
        var source = new SimulatedMachineDataSource(Options.Create(new SimulationOptions { Profiles = [LowSpeed] }), new FixedClock());
        Machine machine = RunningMachine(LowSpeed);
        MachineReading first = await source.ReadAsync(machine, token);
        MachineReading current = first;
        var speeds = new HashSet<int>();

        for (int index = 0; index < 180; index++)
        {
            current = await source.ReadAsync(machine, token);
            speeds.Add(current.Rpm);
            Assert.InRange(current.Rpm, 1_152, 1_248);
            Assert.InRange(current.Vibration, 0.8, 1.2);
            Assert.InRange(current.Torque, 11, 19);
            Assert.InRange(current.ToolWear, 0, 1);
        }

        Assert.InRange(first.Temperature, LowSpeed.AmbientTemperature, LowSpeed.AmbientTemperature + 3);
        Assert.InRange(current.Temperature, LowSpeed.OperatingTemperature - 1, LowSpeed.OperatingTemperature);
        Assert.True(current.ToolWear > first.ToolWear);
        Assert.True(speeds.Count > 1);
        Assert.Equal(machine.Id, first.MachineId);
        Assert.Equal(Epoch.UtcDateTime, first.TimestampUtc);
        Assert.Equal(DateTimeKind.Utc, first.TimestampUtc.Kind);
    }

    [Theory]
    [InlineData(MachineStatus.Idle)]
    [InlineData(MachineStatus.Offline)]
    public async Task StoppedMachineCoolsAndKeepsItsWear(MachineStatus status)
    {
        CancellationToken token = TestContext.Current.CancellationToken;
        var source = new SimulatedMachineDataSource(Options.Create(new SimulationOptions { Profiles = [LowSpeed] }), new FixedClock());
        Machine machine = RunningMachine(LowSpeed);
        MachineReading running = await source.ReadAsync(machine, token);
        machine.ChangeStatus(status, Epoch.UtcDateTime);

        MachineReading stopped = await source.ReadAsync(machine, token);
        MachineReading later = await source.ReadAsync(machine, token);

        Assert.InRange(later.Temperature, LowSpeed.AmbientTemperature, stopped.Temperature);
        Assert.True(stopped.Temperature < running.Temperature);
        Assert.Equal(0, later.Rpm);
        Assert.Equal(0, later.Vibration);
        Assert.Equal(0, later.Torque);
        Assert.Equal(running.ToolWear, later.ToolWear);
    }

    [Fact]
    public async Task SeedReproducesReadingsRegardlessOfOtherMachineSamples()
    {
        CancellationToken token = TestContext.Current.CancellationToken;
        IOptions<SimulationOptions> options = Options.Create(new SimulationOptions { Seed = 81, Profiles = [LowSpeed, HighSpeed] });
        var baseline = new SimulatedMachineDataSource(options, new FixedClock());
        var interleaved = new SimulatedMachineDataSource(options, new FixedClock());
        Machine first = RunningMachine(LowSpeed);
        Machine second = RunningMachine(HighSpeed);

        for (int index = 0; index < 12; index++)
        {
            MachineReading other = await interleaved.ReadAsync(second, token);
            MachineReading expected = await baseline.ReadAsync(first, token);
            MachineReading actual = await interleaved.ReadAsync(first, token);

            Assert.InRange(other.Rpm, 3_360, 3_640);
            Assert.Equal((expected.Temperature, expected.Vibration, expected.Rpm, expected.Torque, expected.ToolWear),
                (actual.Temperature, actual.Vibration, actual.Rpm, actual.Torque, actual.ToolWear));
        }
    }

    [Fact]
    public void OptionsRejectInvalidCadenceAndDuplicateProfiles()
    {
        var validator = new SimulationOptionsValidator();
        Assert.True(validator.Validate(null, new SimulationOptions { Profiles = [LowSpeed, HighSpeed] }).Succeeded);
        Assert.True(validator.Validate(null, new SimulationOptions { SamplingInterval = TimeSpan.Zero, Profiles = [LowSpeed] }).Failed);
        Assert.True(validator.Validate(null, new SimulationOptions { Profiles = [LowSpeed, LowSpeed] }).Failed);
    }

    private static Machine RunningMachine(SimulationProfile profile)
    {
        var machine = new Machine(profile.Code, profile.Code, "CNC", "Simulation", Epoch.UtcDateTime);
        machine.ChangeStatus(MachineStatus.Running, Epoch.UtcDateTime);
        return machine;
    }

    private static SimulationProfile Profile(string code, int rpm) => new()
    {
        Id = new Guid(rpm, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0),
        Code = code,
        Status = MachineStatus.Running,
        TargetRpm = rpm,
        AmbientTemperature = 20,
        OperatingTemperature = 70,
        RunningVibration = 1,
        RatedTorque = 20,
        ToolWearPerHour = 0.1
    };

    private sealed class FixedClock : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => Epoch;
    }
}
