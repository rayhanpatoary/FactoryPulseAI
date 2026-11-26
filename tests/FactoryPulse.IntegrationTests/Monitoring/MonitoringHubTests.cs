using System.Net.Http.Json;
using System.Text.Json;
using System.Threading.Channels;
using FactoryPulse.Api.Realtime;
using FactoryPulse.Application.Monitoring;
using FactoryPulse.Domain.Monitoring;
using FactoryPulse.Infrastructure.Persistence;
using FactoryPulse.IntegrationTests.Persistence;
using FactoryPulse.IntegrationTests.Simulation;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http.Connections;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.SignalR;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Xunit;

namespace FactoryPulse.IntegrationTests.Monitoring;

public sealed class MonitoringHubTests(WebApplicationFactory<Program> factory)
    : IClassFixture<WebApplicationFactory<Program>>
{
    private const int ProfileCount = 3;

    [Fact]
    public async Task ConnectedClientReceivesSampledTelemetryBeforeTheScheduledWriteAndDisconnectsCleanly()
    {
        using var timeout = CreateTimeout();
        CancellationToken token = timeout.Token;
        await using SqliteTestDatabase database = await SqliteTestDatabase.OpenAsync(token);
        var clock = new ManualSimulationClock();
        var source = new ControlledSource(clock);
        await using WebApplicationFactory<Program> host = CreateHost(database, clock, source, simulationEnabled: true);
        await using HubConnection connection = CreateConnection(host);
        var readings = Channel.CreateUnbounded<JsonElement>();
        using IDisposable subscription = connection.On<JsonElement>("MachineReadingUpdated", reading => readings.Writer.TryWrite(reading));
        ConnectionProbe probe = host.Services.GetRequiredService<ConnectionProbe>();
        await connection.StartAsync(token);
        string connectionId = await probe.Connected.Task.WaitAsync(token);
        TimeSpan interval = await clock.TimerStarted.Task.WaitAsync(token);

        clock.Advance(interval);
        JsonElement[] firstFrame = await ReadFrameAsync(readings.Reader, token);
        JsonElement firstMachine = Assert.Single(firstFrame, reading => reading.GetProperty("machineCode").GetString() == "CNC-001");
        Assert.NotEqual(Guid.Empty, firstMachine.GetProperty("machineId").GetGuid());
        Assert.Equal("1970-01-01T00:00:00.2Z", firstMachine.GetProperty("timestampUtc").GetString());
        Assert.Equal(45, firstMachine.GetProperty("temperature").GetDouble());
        Assert.Equal(1.25, firstMachine.GetProperty("vibration").GetDouble());
        Assert.Equal(2400, firstMachine.GetProperty("rpm").GetInt32());
        Assert.Equal(18, firstMachine.GetProperty("torque").GetDouble());
        Assert.Equal(0.1, firstMachine.GetProperty("toolWear").GetDouble());
        Assert.Equal("Running", firstMachine.GetProperty("status").GetString());
        await using FactoryPulseDbContext reader = database.CreateContext();
        Assert.Equal(0, await reader.MachineReadings.CountAsync(token));

        clock.Advance(interval);
        JsonElement[] secondFrame = await ReadFrameAsync(readings.Reader, token);
        Assert.All(secondFrame, item => Assert.Equal("1970-01-01T00:00:00.4Z", item.GetProperty("timestampUtc").GetString()));
        Assert.Equal(ProfileCount, secondFrame.Select(item => item.GetProperty("machineId").GetGuid()).Distinct().Count());
        Assert.Equal(ProfileCount, await reader.MachineReadings.CountAsync(token));
        await connection.StopAsync(token);
        Assert.Equal(connectionId, await probe.Disconnected.Task.WaitAsync(token));
    }

    private WebApplicationFactory<Program> CreateHost(
        SqliteTestDatabase database, ManualSimulationClock clock, ControlledSource source, bool simulationEnabled) =>
        factory.WithWebHostBuilder(builder => builder.UseEnvironment("Testing")
            .ConfigureAppConfiguration((_, configuration) => configuration.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Simulation:Enabled"] = simulationEnabled.ToString(),
                ["Simulation:SamplingInterval"] = "00:00:00.200",
                ["Simulation:PersistenceInterval"] = "00:00:00.400",
                ["RealtimeMonitoring:Enabled"] = "true",
                ["AlarmDetection:Enabled"] = "true"
            }))
            .ConfigureTestServices(services =>
            {
                services.AddScoped(_ => database.CreateContext());
                services.AddSingleton<TimeProvider>(clock);
                services.AddSingleton<IMachineDataSource>(source);
                services.AddSingleton<ConnectionProbe>();
                services.AddSingleton<HubLifetimeManager<MonitoringHub>>(provider => provider.GetRequiredService<ConnectionProbe>());
            }));

    private static HubConnection CreateConnection(WebApplicationFactory<Program> host) =>
        new HubConnectionBuilder().WithUrl("https://localhost/hubs/monitoring", options =>
        {
            options.Transports = HttpTransportType.LongPolling;
            options.HttpMessageHandlerFactory = _ => host.Server.CreateHandler();
        }).Build();

    private static CancellationTokenSource CreateTimeout()
    {
        var timeout = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(20));
        return timeout;
    }

    private static async Task<JsonElement[]> ReadFrameAsync(ChannelReader<JsonElement> reader, CancellationToken token)
    {
        var frame = new JsonElement[ProfileCount];
        for (int index = 0; index < frame.Length; index++)
        {
            frame[index] = await reader.ReadAsync(token);
        }

        return frame;
    }

    private sealed class ControlledSource(TimeProvider clock) : IMachineDataSource
    {
        public ValueTask<MachineReading> ReadAsync(Machine machine, CancellationToken cancellationToken) =>
            ValueTask.FromResult(new MachineReading(machine.Id, clock.GetUtcNow().UtcDateTime, 45, 1.25, 2400, 18, 0.1));
    }

    private sealed class ConnectionProbe(ILogger<DefaultHubLifetimeManager<MonitoringHub>> logger)
        : DefaultHubLifetimeManager<MonitoringHub>(logger)
    {
        internal TaskCompletionSource<string> Connected { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        internal TaskCompletionSource<string> Disconnected { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public override async Task OnConnectedAsync(HubConnectionContext connection)
        {
            await base.OnConnectedAsync(connection);
            Connected.TrySetResult(connection.ConnectionId);
        }

        public override async Task OnDisconnectedAsync(HubConnectionContext connection)
        {
            await base.OnDisconnectedAsync(connection);
            Disconnected.TrySetResult(connection.ConnectionId);
        }
    }
}
