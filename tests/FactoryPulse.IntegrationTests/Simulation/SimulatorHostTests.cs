using FactoryPulse.Application.Monitoring;
using FactoryPulse.Domain.Monitoring;
using FactoryPulse.Infrastructure.Persistence;
using FactoryPulse.Infrastructure.Simulation;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace FactoryPulse.IntegrationTests.Simulation;

public sealed class SimulatorHostTests(WebApplicationFactory<Program> factory)
    : IClassFixture<WebApplicationFactory<Program>>
{
    private const int ProfileCount = 3;

    [Theory]
    [InlineData(false, 2)]
    [InlineData(true, 1)]
    public async Task EnabledWorkerPersistsAtConfiguredCadenceOrOnAnAlarmAndDisposesItsTimer(bool triggerAlarm, int writeTick)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(20));
        CancellationToken token = timeout.Token;
        var clock = new ManualSimulationClock();
        var probe = new TelemetryProbe();
        string databasePath = Path.Combine(Path.GetTempPath(), $"factorypulse-sim-{Guid.NewGuid():N}.db");
        var connectionSettings = new SqliteConnectionStringBuilder { DataSource = databasePath, ForeignKeys = true };
        await using var connection = new SqliteConnection(connectionSettings.ToString());
        try
        {
            DbContextOptions<FactoryPulseDbContext> options =
                new DbContextOptionsBuilder<FactoryPulseDbContext>().UseSqlite(connection).Options;
            await using (var migration = new FactoryPulseDbContext(options))
            {
                await migration.Database.MigrateAsync(token);
            }

            await using (WebApplicationFactory<Program> host = CreateEnabledHost(databasePath, clock, probe, triggerAlarm))
            {
                IServiceProvider services = host.Services;
                TimeSpan interval = await clock.TimerStarted.Task.WaitAsync(token);
                Assert.Equal(TimeSpan.FromMilliseconds(200), interval);

                clock.Advance(interval);
                await probe.FirstFrameSampled.Task.WaitAsync(token);
                if (writeTick == 2)
                {
                    clock.Advance(interval);
                }

                Assert.Equal(DateTime.UnixEpoch.AddMilliseconds(200 * writeTick), await probe.FirstPersistedTime.Task.WaitAsync(token));

                await using AsyncServiceScope scope = services.CreateAsyncScope();
                FactoryPulseDbContext database = scope.ServiceProvider.GetRequiredService<FactoryPulseDbContext>();
                List<Machine> machines = await database.Machines.AsNoTracking().OrderBy(machine => machine.MachineCode).ToListAsync(token);
                List<MachineReading> readings = await database.MachineReadings.AsNoTracking().ToListAsync(token);
                Assert.Equal(ProfileCount, readings.Count);
                Assert.Equal(machines.Select(machine => machine.Id).Order(), readings.Select(reading => reading.MachineId).Order());
                Assert.Equal(0, Assert.Single(readings, reading => reading.MachineId == machines[2].Id).Rpm);
                List<MachineAlarm> alarms = await database.MachineAlarms.AsNoTracking().ToListAsync(token);
                if (triggerAlarm)
                {
                    MachineAlarm alarm = Assert.Single(alarms);
                    Assert.Equal(machines[1].Id, alarm.MachineId);
                    Assert.Equal(AlarmCodes.TemperatureWarning, alarm.AlarmCode);
                    Assert.True(alarm.IsActive);
                }
                else
                {
                    Assert.Empty(alarms);
                }
            }

            Assert.True(clock.TimerStopped.Task.IsCompletedSuccessfully);
        }
        finally
        {
            SqliteConnection.ClearPool(connection);
            File.Delete(databasePath);
            File.Delete(databasePath + "-shm");
            File.Delete(databasePath + "-wal");
            File.Delete(databasePath + "-journal");
        }
    }

    private WebApplicationFactory<Program> CreateEnabledHost(string databasePath, ManualSimulationClock clock, TelemetryProbe probe, bool triggerAlarm)
    {
        return factory.WithWebHostBuilder(builder => builder.UseEnvironment("Testing")
            .ConfigureAppConfiguration((_, configuration) => configuration.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Database:Path"] = databasePath,
                ["Simulation:Enabled"] = "true",
                ["Simulation:SamplingInterval"] = "00:00:00.200",
                ["Simulation:PersistenceInterval"] = "00:00:00.400",
                ["AlarmDetection:TemperatureWarning"] = triggerAlarm ? "23.5" : "80",
                ["AlarmDetection:TemperatureWarningClear"] = triggerAlarm ? "21" : "77"
            }))
            .ConfigureTestServices(services =>
            {
                services.AddSingleton<TimeProvider>(clock);
                services.AddSingleton<SimulatedMachineDataSource>();
                services.AddSingleton<IMachineDataSource>(provider => new ObservedSource(
                    provider.GetRequiredService<SimulatedMachineDataSource>(), probe));
                services.ConfigureDbContext<FactoryPulseDbContext>(options => options.AddInterceptors(probe));
            }));
    }

    private sealed class ObservedSource(SimulatedMachineDataSource source, TelemetryProbe probe) : IMachineDataSource
    {
        public async ValueTask<MachineReading> ReadAsync(Machine machine, CancellationToken cancellationToken)
        {
            MachineReading reading = await source.ReadAsync(machine, cancellationToken);
            probe.RecordSample();
            return reading;
        }
    }

    private sealed class TelemetryProbe : SaveChangesInterceptor
    {
        private int readCount;

        internal TaskCompletionSource FirstFrameSampled { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        internal TaskCompletionSource<DateTime> FirstPersistedTime { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        internal void RecordSample()
        {
            if (++readCount == ProfileCount)
            {
                FirstFrameSampled.TrySetResult();
            }
        }

        public override ValueTask<int> SavedChangesAsync(
            SaveChangesCompletedEventData eventData, int result, CancellationToken cancellationToken = default)
        {
            MachineReading? reading = eventData.Context?.ChangeTracker.Entries<MachineReading>().FirstOrDefault()?.Entity;
            if (reading is not null)
            {
                FirstPersistedTime.TrySetResult(reading.TimestampUtc);
            }

            return ValueTask.FromResult(result);
        }
    }
}
