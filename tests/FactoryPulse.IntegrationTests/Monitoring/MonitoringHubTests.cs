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

[CollectionDefinition(nameof(SimulatorWireTests), DisableParallelization = true)]
public sealed class SimulatorWireTests;

// These virtual-clock cases still exercise the publisher's real transport deadline.
[Collection(nameof(SimulatorWireTests))]
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

    [Fact]
    public async Task AlarmEventsCarryCommittedIdsAndRepeatedReadingsDoNotDuplicateOpenings()
    {
        using var timeout = CreateTimeout();
        CancellationToken token = timeout.Token;
        await using SqliteTestDatabase database = await SqliteTestDatabase.OpenAsync(token);
        var clock = new ManualSimulationClock();
        var source = new ControlledSource(clock) { FirstMachineTemperature = 85 };
        await using WebApplicationFactory<Program> host = CreateHost(database, clock, source, simulationEnabled: true);
        using HttpClient api = host.CreateClient(new WebApplicationFactoryClientOptions { BaseAddress = new Uri("https://localhost") });
        await using HubConnection connection = CreateConnection(host);
        var messages = Channel.CreateUnbounded<(string Name, JsonElement Payload)>();
        using IDisposable openedSubscription = Subscribe(connection, messages.Writer, "AlarmOpened");
        using IDisposable resolvedSubscription = Subscribe(connection, messages.Writer, "AlarmResolved");
        using IDisposable readingSubscription = Subscribe(connection, messages.Writer, "MachineReadingUpdated");
        await connection.StartAsync(token);
        await host.Services.GetRequiredService<ConnectionProbe>().Connected.Task.WaitAsync(token);
        TimeSpan interval = await clock.TimerStarted.Task.WaitAsync(token);

        clock.Advance(interval);
        (string openedName, JsonElement opened) = await messages.Reader.ReadAsync(token);
        Assert.Equal("AlarmOpened", openedName);
        long id = opened.GetProperty("alarmId").GetInt64();
        Assert.True(id > 0);
        Assert.Equal("TEMPERATURE_WARNING", opened.GetProperty("alarmCode").GetString());
        Assert.Equal("Warning", opened.GetProperty("severity").GetString());
        Assert.Equal("1970-01-01T00:00:00.2Z", opened.GetProperty("createdAtUtc").GetString());
        Assert.True(opened.GetProperty("isActive").GetBoolean());
        Assert.False(opened.GetProperty("isAcknowledged").GetBoolean());
        JsonElement active = await api.GetFromJsonAsync<JsonElement>("/api/alarms/active", token);
        Assert.Equal(id, Assert.Single(active.GetProperty("items").EnumerateArray()).GetProperty("id").GetInt64());
        await ExpectReadingsAsync();

        clock.Advance(interval);
        await ExpectReadingsAsync();

        source.FirstMachineTemperature = 76;
        clock.Advance(interval);
        (string resolvedName, JsonElement resolved) = await messages.Reader.ReadAsync(token);
        Assert.Equal("AlarmResolved", resolvedName);
        Assert.Equal(id, resolved.GetProperty("alarmId").GetInt64());
        Assert.Equal(opened.GetProperty("machineId").GetGuid(), resolved.GetProperty("machineId").GetGuid());
        Assert.Equal("TEMPERATURE_WARNING", resolved.GetProperty("alarmCode").GetString());
        Assert.Equal("1970-01-01T00:00:00.6Z", resolved.GetProperty("resolvedAtUtc").GetString());
        JsonElement history = await api.GetFromJsonAsync<JsonElement>("/api/alarms?active=false", token);
        Assert.Equal(id, Assert.Single(history.GetProperty("items").EnumerateArray()).GetProperty("id").GetInt64());
        await ExpectReadingsAsync();

        async Task ExpectReadingsAsync()
        {
            for (int index = 0; index < ProfileCount; index++)
            {
                Assert.Equal("MachineReadingUpdated", (await messages.Reader.ReadAsync(token)).Name);
            }
        }
    }

    private static IDisposable Subscribe(HubConnection connection, ChannelWriter<(string Name, JsonElement Payload)> writer, string name) =>
        connection.On<JsonElement>(name, payload => writer.TryWrite((name, payload)));

    [Fact]
    public async Task FirstRestAcknowledgementPublishesOnceWithSimulationDisabled()
    {
        using var timeout = CreateTimeout();
        CancellationToken token = timeout.Token;
        await using SqliteTestDatabase database = await SqliteTestDatabase.OpenAsync(token);
        var machine = new Machine("CNC-001", "Mill", "CNC", "Test", DateTime.UnixEpoch);
        var alarm = new MachineAlarm(machine.Id, AlarmCodes.VibrationWarning, "Vibration", AlarmSeverity.Warning, DateTime.UnixEpoch);
        await using (FactoryPulseDbContext writer = database.CreateContext())
        {
            writer.Machines.Add(machine);
            writer.MachineAlarms.Add(alarm);
            await writer.SaveChangesAsync(token);
        }

        var clock = new ManualSimulationClock();
        await using WebApplicationFactory<Program> host = CreateHost(database, clock, new ControlledSource(clock), simulationEnabled: false);
        using HttpClient api = host.CreateClient(new WebApplicationFactoryClientOptions { BaseAddress = new Uri("https://localhost") });
        await using HubConnection connection = CreateConnection(host);
        var messages = Channel.CreateUnbounded<(string Name, JsonElement Payload)>();
        using IDisposable acknowledgementSubscription = Subscribe(connection, messages.Writer, "AlarmAcknowledged");
        using IDisposable readingSubscription = Subscribe(connection, messages.Writer, "MachineReadingUpdated");
        await connection.StartAsync(token);
        await host.Services.GetRequiredService<ConnectionProbe>().Connected.Task.WaitAsync(token);

        string route = $"/api/alarms/{alarm.Id}/acknowledge";
        using HttpResponseMessage first = await api.PostProtectedAsync(route, token);
        first.EnsureSuccessStatusCode();
        (string name, JsonElement payload) = await messages.Reader.ReadAsync(token);
        Assert.Equal("AlarmAcknowledged", name);
        Assert.Equal(alarm.Id, payload.GetProperty("alarmId").GetInt64());
        Assert.Equal(machine.Id, payload.GetProperty("machineId").GetGuid());
        Assert.Equal("1970-01-01T00:00:00Z", payload.GetProperty("acknowledgedAtUtc").GetString());
        Assert.True(payload.GetProperty("isAcknowledged").GetBoolean());
        JsonElement current = await api.GetFromJsonAsync<JsonElement>("/api/alarms/active", token);
        Assert.Equal("ActiveAcknowledged", Assert.Single(current.GetProperty("items").EnumerateArray()).GetProperty("state").GetString());

        using HttpResponseMessage repeat = await api.PostProtectedAsync(route, token);
        repeat.EnsureSuccessStatusCode();
        // A later broadcast marks the end of the repeat request without a timed absence assertion.
        IRealtimeMonitoringPublisher publisher = host.Services.GetRequiredService<IRealtimeMonitoringPublisher>();
        await publisher.PublishAsync([new MachineReadingUpdated(machine.Id, machine.MachineCode, DateTime.UnixEpoch,
            45, 1.25, 2400, 18, 0.1, machine.Status)], token);
        Assert.Equal("MachineReadingUpdated", (await messages.Reader.ReadAsync(token)).Name);
        Assert.False(clock.TimerStarted.Task.IsCompleted);
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
                services.AddMonitoringTestSession();
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
        internal double FirstMachineTemperature { get; set; } = 45;

        public ValueTask<MachineReading> ReadAsync(Machine machine, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            double temperature = machine.MachineCode == "CNC-001" ? FirstMachineTemperature : 45;
            return ValueTask.FromResult(new MachineReading(machine.Id, clock.GetUtcNow().UtcDateTime, temperature, 1.25, 2400, 18, 0.1));
        }
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
