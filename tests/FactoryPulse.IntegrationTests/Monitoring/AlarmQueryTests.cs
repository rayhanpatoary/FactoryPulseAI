using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FactoryPulse.Application.Monitoring;
using Xunit;

namespace FactoryPulse.IntegrationTests.Monitoring;

public sealed class AlarmQueryTests(MachineApiFixture fixture) : IClassFixture<MachineApiFixture>
{
    [Fact]
    public async Task ListReturnsBoundedNewestFirstHistory()
    {
        CancellationToken token = TestContext.Current.CancellationToken;
        JsonElement first = await fixture.Client.GetFromJsonAsync<JsonElement>("/api/alarms?pageSize=1", token);
        JsonElement second = await fixture.Client.GetFromJsonAsync<JsonElement>("/api/alarms?page=2&pageSize=1", token);

        Assert.Equal(1, first.GetProperty("pageSize").GetInt32());
        Assert.True(first.GetProperty("hasMore").GetBoolean());
        Assert.Equal(AlarmCodes.VibrationWarning, Assert.Single(first.GetProperty("items").EnumerateArray()).GetProperty("alarmCode").GetString());
        JsonElement resolved = Assert.Single(second.GetProperty("items").EnumerateArray());
        Assert.Equal("Resolved", resolved.GetProperty("state").GetString());
        Assert.Equal("Critical", resolved.GetProperty("severity").GetString());
        Assert.Equal("2026-01-05T08:03:00Z", resolved.GetProperty("resolvedAtUtc").GetString());
    }

    [Fact]
    public async Task ActiveIncludesAcknowledgedAlarmsAndExcludesResolvedAlarms()
    {
        JsonElement active = await fixture.Client.GetFromJsonAsync<JsonElement>("/api/alarms/active", TestContext.Current.CancellationToken);

        Assert.Collection(active.GetProperty("items").EnumerateArray(),
            item => Assert.Equal("ActiveAcknowledged", item.GetProperty("state").GetString()),
            item => Assert.Equal("ActiveUnacknowledged", item.GetProperty("state").GetString()));
    }

    [Theory]
    [InlineData("severity=Critical", AlarmCodes.TemperatureCritical)]
    [InlineData("active=false", AlarmCodes.TemperatureCritical)]
    [InlineData("code=VIBRATION_WARNING", AlarmCodes.VibrationWarning)]
    public async Task ListFiltersAlarmHistory(string query, string expectedCode)
    {
        JsonElement page = await fixture.Client.GetFromJsonAsync<JsonElement>($"/api/alarms?{query}", TestContext.Current.CancellationToken);

        Assert.Equal(expectedCode, Assert.Single(page.GetProperty("items").EnumerateArray()).GetProperty("alarmCode").GetString());
    }

    [Fact]
    public async Task BothMachineFiltersMatchRelationshipsAndUnknownMachineReturns404()
    {
        CancellationToken token = TestContext.Current.CancellationToken;
        Guid id = MachineApiFixture.RunningId;
        JsonElement filtered = await fixture.Client.GetFromJsonAsync<JsonElement>($"/api/alarms?machineId={id}", token);
        JsonElement scoped = await fixture.Client.GetFromJsonAsync<JsonElement>($"/api/machines/{id}/alarms", token);
        using HttpResponseMessage missing = await fixture.Client.GetAsync($"/api/machines/{MachineApiFixture.UnknownId}/alarms", token);
        JsonElement empty = await fixture.Client.GetFromJsonAsync<JsonElement>($"/api/machines/{MachineApiFixture.IdleId}/alarms", token);

        JsonElement.ArrayEnumerator alarms = scoped.GetProperty("items").EnumerateArray();
        Assert.Equal(2, scoped.GetProperty("items").GetArrayLength());
        Assert.All(alarms, item => Assert.Equal(id, item.GetProperty("machineId").GetGuid()));
        Assert.Equal(filtered.GetProperty("items").GetRawText(), scoped.GetProperty("items").GetRawText());
        Assert.Empty(empty.GetProperty("items").EnumerateArray());
        Assert.Equal(HttpStatusCode.NotFound, missing.StatusCode);
    }

    [Theory]
    [InlineData("page=0")]
    [InlineData("pageSize=101")]
    [InlineData("severity=Unknown")]
    [InlineData("machineId=invalid")]
    public async Task InvalidParametersReturnProblemDetails(string query)
    {
        CancellationToken token = TestContext.Current.CancellationToken;
        using HttpResponseMessage response = await fixture.Client.GetAsync($"/api/alarms?{query}", token);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
    }
}
