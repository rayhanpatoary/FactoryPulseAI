using System.Net;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Xunit;

namespace FactoryPulse.IntegrationTests.Monitoring;

public sealed class MonitoringEndpointTests(WebApplicationFactory<Program> factory)
    : IClassFixture<WebApplicationFactory<Program>>
{
    [Theory]
    [InlineData("Development", true, null, HttpStatusCode.OK)]
    [InlineData("Testing", true, "https://localhost", HttpStatusCode.OK)]
    [InlineData("Testing", true, "https://other.example", HttpStatusCode.Forbidden)]
    [InlineData("Testing", true, "null", HttpStatusCode.Forbidden)]
    [InlineData("Testing", false, null, HttpStatusCode.NotFound)]
    [InlineData("Production", true, null, HttpStatusCode.NotFound)]
    [InlineData("Staging", true, null, HttpStatusCode.NotFound)]
    public async Task NegotiationRequiresAnAllowedEnvironmentAndBrowserOrigin(
        string environment, bool enabled, string? origin, HttpStatusCode status)
    {
        await using WebApplicationFactory<Program> host = factory.WithWebHostBuilder(builder => builder.UseEnvironment(environment)
            .ConfigureAppConfiguration((_, configuration) => configuration.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["RealtimeMonitoring:Enabled"] = enabled.ToString(),
                ["Simulation:Enabled"] = "false"
            })).ConfigureTestServices(services => services.AddMonitoringTestSession()));
        using HttpClient client = host.CreateClient(new WebApplicationFactoryClientOptions { BaseAddress = new Uri("https://localhost") });
        using var request = new HttpRequestMessage(HttpMethod.Post, "/hubs/monitoring/negotiate?negotiateVersion=1");
        if (origin is not null)
        {
            request.Headers.Add("Origin", origin);
        }

        using HttpResponseMessage response = await client.SendAsync(request, TestContext.Current.CancellationToken);

        Assert.Equal(status, response.StatusCode);
    }
}
