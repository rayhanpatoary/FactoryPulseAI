using FactoryPulse.Api.Realtime;
using FactoryPulse.Application.Monitoring;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.SignalR;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Xunit;

namespace FactoryPulse.IntegrationTests.Monitoring;

public sealed class RealtimePublisherTests(WebApplicationFactory<Program> factory)
    : IClassFixture<WebApplicationFactory<Program>>
{
    private static readonly AlarmAcknowledged Acknowledgement = new(1, Guid.Empty, DateTime.UnixEpoch, true);

    [Fact]
    public async Task BroadcastFailureDoesNotPreventSubsequentPublishes()
    {
        await using WebApplicationFactory<Program> host = CreateHost(enabled: true);
        IRealtimeMonitoringPublisher publisher = host.Services.GetRequiredService<IRealtimeMonitoringPublisher>();
        SendProbe probe = host.Services.GetRequiredService<SendProbe>();
        probe.Fail = true;

        await publisher.PublishAsync([Acknowledgement], TestContext.Current.CancellationToken);
        probe.Fail = false;
        await publisher.PublishAsync([Acknowledgement], TestContext.Current.CancellationToken);

        Assert.Equal(2, probe.Attempts);
        Assert.Equal("AlarmAcknowledged", probe.Name);
        Assert.Same(Acknowledgement, probe.Payload);
    }

    [Fact]
    public async Task DisabledPublisherDoesNotSend()
    {
        await using WebApplicationFactory<Program> host = CreateHost(enabled: false);
        IRealtimeMonitoringPublisher publisher = host.Services.GetRequiredService<IRealtimeMonitoringPublisher>();

        await publisher.PublishAsync([Acknowledgement], TestContext.Current.CancellationToken);

        Assert.False(publisher.IsEnabled);
        Assert.Equal(0, host.Services.GetRequiredService<SendProbe>().Attempts);
    }

    [Fact]
    public async Task PublisherPropagatesCallerCancellation()
    {
        await using WebApplicationFactory<Program> host = CreateHost(enabled: true);
        IRealtimeMonitoringPublisher publisher = host.Services.GetRequiredService<IRealtimeMonitoringPublisher>();
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => publisher.PublishAsync([Acknowledgement], cancellation.Token));
        Assert.Equal(0, host.Services.GetRequiredService<SendProbe>().Attempts);
    }

    private WebApplicationFactory<Program> CreateHost(bool enabled) =>
        factory.WithWebHostBuilder(builder => builder.UseEnvironment("Testing")
            .ConfigureAppConfiguration((_, configuration) => configuration.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["RealtimeMonitoring:Enabled"] = enabled.ToString(),
                ["Simulation:Enabled"] = "false"
            }))
            .ConfigureTestServices(services =>
            {
                services.AddSingleton<SendProbe>();
                services.AddSingleton<HubLifetimeManager<MonitoringHub>>(provider => provider.GetRequiredService<SendProbe>());
            }));

    private sealed class SendProbe(ILogger<DefaultHubLifetimeManager<MonitoringHub>> logger)
        : DefaultHubLifetimeManager<MonitoringHub>(logger)
    {
        internal bool Fail { get; set; }
        internal int Attempts { get; private set; }
        internal string? Name { get; private set; }
        internal object? Payload { get; private set; }

        public override Task SendAllAsync(string methodName, object?[] args, CancellationToken cancellationToken = default)
        {
            Attempts++;
            if (Fail)
            {
                return Task.FromException(new IOException("Simulated transport failure."));
            }

            Name = methodName;
            Payload = Assert.Single(args);
            return Task.CompletedTask;
        }
    }
}
