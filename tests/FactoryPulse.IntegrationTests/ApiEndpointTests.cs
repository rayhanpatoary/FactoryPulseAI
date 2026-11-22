using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;
using Xunit;

namespace FactoryPulse.IntegrationTests;

public sealed class ApiEndpointTests(WebApplicationFactory<Program> factory)
    : IClassFixture<WebApplicationFactory<Program>>
{
    private static readonly WebApplicationFactoryClientOptions ClientOptions = new()
    {
        BaseAddress = new Uri("https://localhost"),
        AllowAutoRedirect = false
    };

    [Fact]
    public async Task HealthEndpointReturnsOkAndHealthy()
    {
        await using WebApplicationFactory<Program> application = CreateApplication("Testing");
        using HttpClient client = application.CreateClient(ClientOptions);
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;

        using HttpResponseMessage response = await client.GetAsync("/health", cancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("Healthy", await response.Content.ReadAsStringAsync(cancellationToken));
    }

    [Fact]
    public async Task StatusEndpointReturnsConfiguredSimulationStatus()
    {
        await using WebApplicationFactory<Program> application = CreateApplication("Testing");
        using HttpClient client = application.CreateClient(ClientOptions);
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;

        using HttpResponseMessage response = await client.GetAsync("/api/status", cancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using JsonDocument? payload = await response.Content.ReadFromJsonAsync<JsonDocument>(cancellationToken);
        Assert.NotNull(payload);
        Assert.Equal("Integration Test Instance", payload.RootElement.GetProperty("instanceName").GetString());
        Assert.Equal("simulation", payload.RootElement.GetProperty("operatingMode").GetString());
        Assert.Equal(TimeSpan.Zero, payload.RootElement.GetProperty("observedAtUtc").GetDateTimeOffset().Offset);
    }

    [Theory]
    [InlineData("Development", HttpStatusCode.OK)]
    [InlineData("Testing", HttpStatusCode.NotFound)]
    public async Task OpenApiEndpointIsAvailableOnlyInDevelopment(string environment, HttpStatusCode expectedStatus)
    {
        await using WebApplicationFactory<Program> application = CreateApplication(environment);
        using HttpClient client = application.CreateClient(ClientOptions);

        using HttpResponseMessage response = await client.GetAsync("/openapi/v1.json", TestContext.Current.CancellationToken);

        Assert.Equal(expectedStatus, response.StatusCode);
    }

    [Fact]
    public async Task StartupRejectsBlankInstanceName()
    {
        await using WebApplicationFactory<Program> application = CreateApplication("Testing", " ");

        OptionsValidationException exception = Assert.Throws<OptionsValidationException>(() => application.CreateClient(ClientOptions));

        Assert.Contains("ApplicationStatus:InstanceName", exception.Message, StringComparison.Ordinal);
    }

    private WebApplicationFactory<Program> CreateApplication(string environment, string instanceName = "Integration Test Instance")
    {
        return factory.WithWebHostBuilder(builder => builder.UseEnvironment(environment)
            .ConfigureAppConfiguration((_, configuration) => configuration.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ApplicationStatus:InstanceName"] = instanceName
            })));
    }
}
