using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FactoryPulse.Infrastructure.Persistence;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace FactoryPulse.IntegrationTests.Monitoring;

public sealed class MachineCatalogTests(MachineApiFixture fixture) : IClassFixture<MachineApiFixture>
{
    [Fact]
    public async Task ListReturnsOrderedMachineSummaries()
    {
        JsonElement machines = await fixture.Client.GetFromJsonAsync<JsonElement>(
            "/api/machines", TestContext.Current.CancellationToken);

        Assert.Collection(machines.EnumerateArray(),
            machine =>
            {
                Assert.Equal(MachineApiFixture.RunningId, machine.GetProperty("id").GetGuid());
                Assert.Equal("CNC-001", machine.GetProperty("machineCode").GetString());
                Assert.Equal("Running", machine.GetProperty("status").GetString());
                Assert.False(machine.TryGetProperty("description", out _));
            },
            machine => Assert.Equal("CNC-002", machine.GetProperty("machineCode").GetString()),
            machine => Assert.Equal("CNC-003", machine.GetProperty("machineCode").GetString()));
    }

    [Fact]
    public async Task DetailReturnsMetadataAndUtcTimestamps()
    {
        JsonElement machine = await fixture.Client.GetFromJsonAsync<JsonElement>(
            $"/api/machines/{MachineApiFixture.RunningId}", TestContext.Current.CancellationToken);

        Assert.Equal("Mill A", machine.GetProperty("name").GetString());
        Assert.Equal("Primary production mill", machine.GetProperty("description").GetString());
        Assert.Equal("CNC", machine.GetProperty("machineType").GetString());
        Assert.Equal("Line A", machine.GetProperty("location").GetString());
        DateTime created = machine.GetProperty("createdAtUtc").GetDateTime();
        Assert.Equal(MachineApiFixture.StartUtc, created);
        Assert.Equal(DateTimeKind.Utc, created.Kind);
        Assert.Equal(MachineApiFixture.StartUtc.AddMinutes(1), machine.GetProperty("updatedAtUtc").GetDateTime());
    }

    [Theory]
    [InlineData(false, HttpStatusCode.NotFound)]
    [InlineData(true, HttpStatusCode.BadRequest)]
    public async Task DetailReturnsProblemDetailsForUnknownOrMalformedIds(bool malformed, HttpStatusCode expected)
    {
        CancellationToken token = TestContext.Current.CancellationToken;
        string id = malformed ? "invalid-id" : MachineApiFixture.UnknownId.ToString();
        using HttpResponseMessage response = await fixture.Client.GetAsync($"/api/machines/{id}", token);

        Assert.Equal(expected, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        JsonElement problem = await response.Content.ReadFromJsonAsync<JsonElement>(token);
        Assert.Equal((int)expected, problem.GetProperty("status").GetInt32());
    }

    [Fact]
    public async Task SummaryAggregatesStatusesAndIncludesZeroCounts()
    {
        JsonElement summary = await fixture.Client.GetFromJsonAsync<JsonElement>(
            "/api/machines/summary", TestContext.Current.CancellationToken);

        Assert.Equal(3, summary.GetProperty("total").GetInt64());
        Assert.Equal(2, summary.GetProperty("running").GetInt64());
        Assert.Equal(1, summary.GetProperty("idle").GetInt64());
        Assert.Equal(0, summary.GetProperty("offline").GetInt64());
        Assert.Equal(0, summary.GetProperty("faulted").GetInt64());
    }

    [Fact]
    public async Task UnexpectedErrorsReturnProblemDetailsWithoutInternalMessages()
    {
        const string InternalMessage = "Internal machine query failure marker.";
        await using var factory = new WebApplicationFactory<Program>();
        await using WebApplicationFactory<Program> host = factory.WithWebHostBuilder(builder => builder.UseEnvironment("Production")
            .ConfigureAppConfiguration((_, configuration) => configuration.AddInMemoryCollection(
                new Dictionary<string, string?> { ["Simulation:Enabled"] = "false" }))
            .ConfigureTestServices(services =>
            {
                services.AddMonitoringTestSession();
                services.AddScoped<FactoryPulseDbContext>(_ => throw new InvalidOperationException(InternalMessage));
            }));
        using HttpClient client = host.CreateClient(new WebApplicationFactoryClientOptions { BaseAddress = new Uri("https://localhost") });
        CancellationToken token = TestContext.Current.CancellationToken;
        using HttpResponseMessage response = await client.GetAsync("/api/machines", token);
        string body = await response.Content.ReadAsStringAsync(token);

        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        Assert.DoesNotContain(InternalMessage, body, StringComparison.Ordinal);
        using var problem = JsonDocument.Parse(body);
        Assert.Equal(500, problem.RootElement.GetProperty("status").GetInt32());
    }
}
