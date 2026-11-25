using FactoryPulse.Application.Monitoring;
using FactoryPulse.Domain.Monitoring;
using FactoryPulse.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace FactoryPulse.IntegrationTests.Persistence;

public sealed class TelemetryProcessorTests
{
    [Fact]
    public async Task TransitionsPersistImmediatelyAndKeepIndependentHistoryAcrossRestart()
    {
        CancellationToken token = TestContext.Current.CancellationToken;
        DateTime start = DateTime.UnixEpoch;
        await using SqliteTestDatabase database = await SqliteTestDatabase.OpenAsync(token);
        var first = new Machine("CNC-FIRST", "First mill", "CNC", "Test", start);
        var second = new Machine("CNC-SECOND", "Second mill", "CNC", "Test", start);
        await using (FactoryPulseDbContext writer = database.CreateContext())
        {
            writer.Machines.AddRange(first, second);
            await writer.SaveChangesAsync(token);
        }

        var services = new ServiceCollection();
        services.AddScoped(_ => database.CreateContext());
        services.AddScoped<TelemetryStore>();
        await using ServiceProvider provider = services.BuildServiceProvider();
        IServiceScopeFactory scopes = provider.GetRequiredService<IServiceScopeFactory>();
        var detector = new AlarmDetectionService(new AlarmDetectionOptions());
        var processor = new TelemetryProcessor(detector, scopes);
        Guid[] machineIds = [first.Id, second.Id];
        await processor.InitializeAsync(machineIds, token);
        Assert.False(await Tick(processor, first, 0, 70, 1));
        Assert.True(await Tick(processor, first, 1, 80, 5));
        Assert.False(await Tick(processor, first, 2, 81, 5));

        await using (FactoryPulseDbContext writer = database.CreateContext())
        {
            MachineAlarm warning = await writer.MachineAlarms.SingleAsync(alarm => alarm.AlarmCode == AlarmCodes.TemperatureWarning, token);
            warning.Acknowledge(start.AddSeconds(2));
            await writer.SaveChangesAsync(token);
        }

        var restarted = new TelemetryProcessor(detector, scopes);
        await restarted.InitializeAsync(machineIds, token);
        Assert.False(await Tick(restarted, first, 3, 78, 4.5));
        Assert.True(await Tick(restarted, first, 4, 95, 3.9));
        Assert.False(await Tick(restarted, first, 3, 60, 0));
        Assert.True(await Tick(restarted, first, 5, 76, 0));
        Assert.True(await Tick(restarted, second, 6, 80, 1));

        await using FactoryPulseDbContext reader = database.CreateContext();
        List<MachineAlarm> history = await reader.MachineAlarms.AsNoTracking()
            .OrderBy(alarm => alarm.CreatedAtUtc).ToListAsync(token);
        Assert.Equal(4, history.Count);
        MachineAlarm oldWarning = Assert.Single(history, alarm => alarm.MachineId == first.Id && alarm.AlarmCode == AlarmCodes.TemperatureWarning);
        Assert.Equal(start.AddSeconds(2), oldWarning.AcknowledgedAtUtc);
        Assert.Equal(start.AddSeconds(4), oldWarning.ResolvedAtUtc);
        Assert.Equal(start.AddSeconds(4), Assert.Single(history, alarm => alarm.AlarmCode == AlarmCodes.VibrationWarning).ResolvedAtUtc);
        Assert.Equal(start.AddSeconds(5), Assert.Single(history, alarm => alarm.Severity == AlarmSeverity.Critical).ResolvedAtUtc);
        Assert.Equal(second.Id, Assert.Single(history, alarm => alarm.IsActive).MachineId);
        Assert.Equal(4, await reader.MachineReadings.CountAsync(token));

        Task<bool> Tick(TelemetryProcessor session, Machine machine, int second, double temperature, double vibration) =>
            session.ProcessAsync([new MachineReading(machine.Id, start.AddSeconds(second), temperature, vibration, 2400, 18, 0.1)],
                persistenceDue: false, token);
    }
}
