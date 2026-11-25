using FactoryPulse.Application.Monitoring;
using FactoryPulse.Infrastructure.Simulation;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Xunit;

namespace FactoryPulse.IntegrationTests.Simulation;

public sealed class SimulatorConfigurationTests(WebApplicationFactory<Program> factory)
    : IClassFixture<WebApplicationFactory<Program>>
{
    private static readonly string[] ExpectedCodes = ["CNC-001", "CNC-002", "CNC-003"];

    [Fact]
    public async Task TestingHostProvidesSourceAndLeavesWorkerDisabledByDefault()
    {
        await using WebApplicationFactory<Program> host = factory.WithWebHostBuilder(builder => builder.UseEnvironment("Testing")
            .ConfigureTestServices(services => services.AddScoped<SimulationStore>(_ =>
                throw new InvalidOperationException("The disabled simulator must not resolve its store."))));
        IServiceProvider services = host.Services;
        SimulationOptions settings = services.GetRequiredService<IOptions<SimulationOptions>>().Value;

        Assert.False(settings.Enabled);
        Assert.Equal(ExpectedCodes, settings.Profiles.Select(profile => profile.Code));
        Assert.IsType<SimulatedMachineDataSource>(services.GetRequiredService<IMachineDataSource>());
        BackgroundService worker = Assert.Single(services.GetServices<IHostedService>().OfType<BackgroundService>());
        Task? execution = worker.ExecuteTask;
        Assert.NotNull(execution);
        await execution.WaitAsync(TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task StartupRejectsInvalidSimulationCadence()
    {
        await using WebApplicationFactory<Program> host = factory.WithWebHostBuilder(builder => builder.UseEnvironment("Testing")
            .ConfigureAppConfiguration((_, configuration) => configuration.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Simulation:PersistenceInterval"] = "00:00:00"
            })));

        OptionsValidationException exception = Assert.Throws<OptionsValidationException>(() => host.CreateClient());
        Assert.Contains("sampling interval", exception.Message, StringComparison.Ordinal);
    }
}
