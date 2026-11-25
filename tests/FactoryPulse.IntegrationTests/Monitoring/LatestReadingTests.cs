using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Xunit;

namespace FactoryPulse.IntegrationTests.Monitoring;

public sealed class LatestReadingTests(MachineApiFixture fixture) : IClassFixture<MachineApiFixture>
{
    [Fact]
    public async Task LatestReturnsReadingForRequestedMachineAndBreaksTimestampTies()
    {
        JsonElement reading = await fixture.Client.GetFromJsonAsync<JsonElement>(
            $"/api/machines/{MachineApiFixture.RunningId}/readings/latest", TestContext.Current.CancellationToken);

        Assert.Equal(MachineApiFixture.RunningId, reading.GetProperty("machineId").GetGuid());
        Assert.Equal("2026-01-05T08:03:00Z", reading.GetProperty("timestampUtc").GetString());
        Assert.Equal(2350, reading.GetProperty("rpm").GetInt32());
        Assert.Equal(50, reading.GetProperty("temperature").GetDouble());
        Assert.Equal(1.1, reading.GetProperty("vibration").GetDouble());
        Assert.Equal(18, reading.GetProperty("torque").GetDouble());
        Assert.Equal(0.07, reading.GetProperty("toolWear").GetDouble());
        Assert.False(reading.TryGetProperty("id", out _));
    }

    [Fact]
    public async Task LatestDistinguishesKnownEmptyMachinesFromUnknownMachines()
    {
        CancellationToken token = TestContext.Current.CancellationToken;
        using HttpResponseMessage empty = await fixture.Client.GetAsync(
            $"/api/machines/{MachineApiFixture.IdleId}/readings/latest", token);
        using HttpResponseMessage unknown = await fixture.Client.GetAsync(
            $"/api/machines/{MachineApiFixture.UnknownId}/readings/latest", token);

        Assert.Equal(HttpStatusCode.NoContent, empty.StatusCode);
        Assert.Empty(await empty.Content.ReadAsStringAsync(token));
        Assert.Equal(HttpStatusCode.NotFound, unknown.StatusCode);
        Assert.Equal("application/problem+json", unknown.Content.Headers.ContentType?.MediaType);
    }
}
