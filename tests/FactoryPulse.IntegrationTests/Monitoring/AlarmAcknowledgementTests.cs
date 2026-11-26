using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FactoryPulse.Api.Security;
using Xunit;

namespace FactoryPulse.IntegrationTests.Monitoring;

public sealed class AlarmAcknowledgementTests(MachineApiFixture fixture) : IClassFixture<MachineApiFixture>
{
    [Theory]
    [InlineData("Production")]
    [InlineData("Staging")]
    public async Task ViewersCannotAcknowledgeAlarms(string environment)
    {
        CancellationToken token = TestContext.Current.CancellationToken;
        using HttpClient client = fixture.CreateClient(environment, role: MonitoringAccess.Viewer);
        using HttpResponseMessage response = await client.PostProtectedAsync(Route(fixture.WarningAlarmId), token);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
    }

    [Fact]
    public async Task AcknowledgementPersistsTheFirstTimestampAndLeavesTheAlarmActive()
    {
        CancellationToken token = TestContext.Current.CancellationToken;
        DateTime now = MachineApiFixture.StartUtc.AddMinutes(15);
        using HttpClient firstClient = fixture.CreateClient("Testing", new FixedClock(now));
        using HttpClient laterClient = fixture.CreateClient("Testing", new FixedClock(now.AddMinutes(2)));

        using HttpResponseMessage firstResponse = await firstClient.PostProtectedAsync(Route(fixture.WarningAlarmId), token);
        using HttpResponseMessage laterResponse = await laterClient.PostProtectedAsync(Route(fixture.WarningAlarmId), token);
        Assert.Equal(HttpStatusCode.OK, firstResponse.StatusCode);
        Assert.Equal(HttpStatusCode.OK, laterResponse.StatusCode);

        JsonElement first = await firstResponse.Content.ReadFromJsonAsync<JsonElement>(token);
        JsonElement later = await laterResponse.Content.ReadFromJsonAsync<JsonElement>(token);
        JsonElement page = await fixture.Client.GetFromJsonAsync<JsonElement>(
            $"/api/alarms/active?machineId={MachineApiFixture.RunningId}", token);
        JsonElement persisted = Assert.Single(page.GetProperty("items").EnumerateArray());

        foreach (JsonElement alarm in new[] { first, later, persisted })
        {
            Assert.Equal("ActiveAcknowledged", alarm.GetProperty("state").GetString());
            Assert.Equal(now, alarm.GetProperty("acknowledgedAtUtc").GetDateTime());
            Assert.Equal(JsonValueKind.Null, alarm.GetProperty("resolvedAtUtc").ValueKind);
        }
    }

    [Fact]
    public async Task DevelopmentCanAcknowledgeResolvedAlarmWithAClockBeforeCreation()
    {
        CancellationToken token = TestContext.Current.CancellationToken;
        using HttpClient client = fixture.CreateClient("Development", new FixedClock(MachineApiFixture.StartUtc));
        using HttpResponseMessage response = await client.PostProtectedAsync(Route(fixture.ResolvedAlarmId), token);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        JsonElement alarm = await response.Content.ReadFromJsonAsync<JsonElement>(token);
        Assert.Equal("Resolved", alarm.GetProperty("state").GetString());
        Assert.Equal(MachineApiFixture.StartUtc.AddMinutes(2), alarm.GetProperty("acknowledgedAtUtc").GetDateTime());
        Assert.Equal(MachineApiFixture.StartUtc.AddMinutes(3), alarm.GetProperty("resolvedAtUtc").GetDateTime());
    }

    [Theory]
    [InlineData("0", HttpStatusCode.BadRequest)]
    [InlineData("invalid", HttpStatusCode.BadRequest)]
    [InlineData("9223372036854775807", HttpStatusCode.NotFound)]
    public async Task InvalidAndMissingIdsReturnProblemDetails(string id, HttpStatusCode statusCode)
    {
        using HttpResponseMessage response = await fixture.Client.PostProtectedAsync(
            $"/api/alarms/{id}/acknowledge", TestContext.Current.CancellationToken);

        Assert.Equal(statusCode, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
    }

    private static string Route(long id) => $"/api/alarms/{id}/acknowledge";

    private sealed class FixedClock(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }
}
