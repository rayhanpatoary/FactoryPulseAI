using FactoryPulse.Api.Security;
using FactoryPulse.Application.Monitoring;
using FactoryPulse.Domain.Monitoring;
using FactoryPulse.Infrastructure.Persistence;
using FactoryPulse.IntegrationTests.Persistence;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace FactoryPulse.IntegrationTests.Monitoring;

public sealed class MachineApiFixture : IAsyncLifetime
{
    internal static readonly Guid RunningId = new("e05831e8-41d0-4ae8-8746-51f890af159e");
    internal static readonly Guid IdleId = new("579cc6cd-9dfe-4333-ac96-b521140af59a");
    internal static readonly Guid OtherId = new("c2041131-9803-4b57-83ce-fc5b36646d96");
    internal static readonly Guid UnknownId = new("500d5a19-77dc-4d72-8474-f8c7391f5512");
    internal static readonly DateTime StartUtc = new(2026, 1, 5, 8, 0, 0, DateTimeKind.Utc);

    private readonly WebApplicationFactory<Program> host = new();
    private SqliteTestDatabase? database;

    internal HttpClient Client { get; private set; } = null!;
    internal long WarningAlarmId { get; private set; }
    internal long ResolvedAlarmId { get; private set; }

    public async ValueTask InitializeAsync()
    {
        CancellationToken token = TestContext.Current.CancellationToken;
        SqliteTestDatabase store = await SqliteTestDatabase.OpenAsync(token);
        database = store;

        await using (FactoryPulseDbContext writer = store.CreateContext())
        {
            var running = new Machine(RunningId, "CNC-001", "Mill A", "CNC", "Line A", StartUtc, "Primary production mill");
            running.ChangeStatus(MachineStatus.Running, StartUtc.AddMinutes(1));
            var other = new Machine(OtherId, "CNC-002", "Mill B", "CNC", "Line A", StartUtc);
            other.ChangeStatus(MachineStatus.Running, StartUtc);
            var idle = new Machine(IdleId, "CNC-003", "Mill C", "CNC", "Line B", StartUtc);
            idle.ChangeStatus(MachineStatus.Idle, StartUtc);
            writer.Machines.AddRange(idle, running, other);
            var warning = new MachineAlarm(RunningId, AlarmCodes.TemperatureWarning, "Temperature warning", AlarmSeverity.Warning, StartUtc.AddMinutes(1));
            var resolved = new MachineAlarm(RunningId, AlarmCodes.TemperatureCritical, "Critical temperature", AlarmSeverity.Critical, StartUtc.AddMinutes(2));
            resolved.Resolve(StartUtc.AddMinutes(3));
            var vibration = new MachineAlarm(OtherId, AlarmCodes.VibrationWarning, "Vibration warning", AlarmSeverity.Warning, StartUtc.AddMinutes(4));
            vibration.Acknowledge(StartUtc.AddMinutes(5));
            writer.MachineAlarms.AddRange(warning, resolved, vibration);
            writer.MachineReadings.AddRange(
                new MachineReading(RunningId, StartUtc.AddMinutes(1), 43, 0.8, 2100, 15, 0.02),
                new MachineReading(RunningId, StartUtc.AddMinutes(2), 46, 0.9, 2200, 16, 0.04),
                new MachineReading(RunningId, StartUtc.AddMinutes(3), 49, 1.0, 2300, 17, 0.06),
                new MachineReading(OtherId, StartUtc.AddMinutes(4), 55, 1.2, 3600, 19, 0.05));
            await writer.SaveChangesAsync(token);
            writer.MachineReadings.Add(new MachineReading(RunningId, StartUtc.AddMinutes(3), 50, 1.1, 2350, 18, 0.07));
            await writer.SaveChangesAsync(token);
            WarningAlarmId = warning.Id;
            ResolvedAlarmId = resolved.Id;
        }

        Client = CreateClient("Testing");
    }

    internal HttpClient CreateClient(string environment, TimeProvider? clock = null, string role = MonitoringAccess.Operator)
    {
        SqliteTestDatabase store = database ?? throw new InvalidOperationException("Initialize the test database before creating a client.");
        return host.WithWebHostBuilder(builder => builder.UseEnvironment(environment)
            .ConfigureAppConfiguration((_, configuration) => configuration.AddInMemoryCollection(
                new Dictionary<string, string?> { ["Simulation:Enabled"] = "false" }))
            .ConfigureTestServices(services =>
            {
                services.AddMonitoringTestSession(role);
                services.AddScoped(_ => store.CreateContext());
                if (clock is not null)
                {
                    services.AddSingleton(clock);
                }
            }))
            .CreateClient(new WebApplicationFactoryClientOptions { BaseAddress = new Uri("https://localhost") });
    }

    public async ValueTask DisposeAsync()
    {
        Client?.Dispose();
        try
        {
            await host.DisposeAsync();
        }
        finally
        {
            if (database is not null)
            {
                await database.DisposeAsync();
            }
        }
    }
}
