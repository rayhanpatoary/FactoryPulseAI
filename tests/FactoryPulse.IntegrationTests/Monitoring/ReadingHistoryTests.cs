using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Xunit;

namespace FactoryPulse.IntegrationTests.Monitoring;

public sealed class ReadingHistoryTests(MachineApiFixture fixture) : IClassFixture<MachineApiFixture>
{
    private static readonly string RunningHistory = $"/api/machines/{MachineApiFixture.RunningId}/readings";

    [Fact]
    public async Task PagesAreBoundedAndKeepStableNewestFirstOrder()
    {
        CancellationToken token = TestContext.Current.CancellationToken;
        JsonElement first = await fixture.Client.GetFromJsonAsync<JsonElement>($"{RunningHistory}?pageSize=2", token);
        JsonElement second = await fixture.Client.GetFromJsonAsync<JsonElement>($"{RunningHistory}?page=2&pageSize=2", token);
        JsonElement beyond = await fixture.Client.GetFromJsonAsync<JsonElement>($"{RunningHistory}?page=3&pageSize=2", token);

        Assert.Equal(1, first.GetProperty("page").GetInt32());
        Assert.Equal(2, first.GetProperty("pageSize").GetInt32());
        Assert.True(first.GetProperty("hasMore").GetBoolean());
        Assert.Collection(first.GetProperty("items").EnumerateArray(),
            reading => Assert.Equal(2350, reading.GetProperty("rpm").GetInt32()),
            reading => Assert.Equal(2300, reading.GetProperty("rpm").GetInt32()));
        Assert.Equal(2, second.GetProperty("page").GetInt32());
        Assert.False(second.GetProperty("hasMore").GetBoolean());
        Assert.Collection(second.GetProperty("items").EnumerateArray(),
            reading => Assert.Equal(2200, reading.GetProperty("rpm").GetInt32()),
            reading => Assert.Equal(2100, reading.GetProperty("rpm").GetInt32()));
        Assert.Empty(beyond.GetProperty("items").EnumerateArray());
        Assert.False(beyond.GetProperty("hasMore").GetBoolean());
    }

    [Fact]
    public async Task DateRangeIncludesLowerBoundAndExcludesUpperBound()
    {
        const string Filter = "?fromUtc=2026-01-05T08:02:00%2B00:00&toUtc=2026-01-05T08:03:00Z";
        JsonElement page = await fixture.Client.GetFromJsonAsync<JsonElement>(
            RunningHistory + Filter, TestContext.Current.CancellationToken);

        JsonElement reading = Assert.Single(page.GetProperty("items").EnumerateArray());
        Assert.Equal(2200, reading.GetProperty("rpm").GetInt32());
        Assert.Equal(25, page.GetProperty("pageSize").GetInt32());
    }

    [Fact]
    public async Task OneSidedFilterAcceptsFractionalUtcTimestamps()
    {
        JsonElement page = await fixture.Client.GetFromJsonAsync<JsonElement>(
            RunningHistory + "?toUtc=2026-01-05T08:01:00.001Z", TestContext.Current.CancellationToken);

        JsonElement reading = Assert.Single(page.GetProperty("items").EnumerateArray());
        Assert.Equal("2026-01-05T08:01:00Z", reading.GetProperty("timestampUtc").GetString());
    }

    [Fact]
    public async Task HistoryDistinguishesAnEmptyMachineFromAnUnknownMachine()
    {
        CancellationToken token = TestContext.Current.CancellationToken;
        JsonElement empty = await fixture.Client.GetFromJsonAsync<JsonElement>(
            $"/api/machines/{MachineApiFixture.IdleId}/readings", token);
        using HttpResponseMessage unknown = await fixture.Client.GetAsync(
            $"/api/machines/{MachineApiFixture.UnknownId}/readings", token);

        Assert.Empty(empty.GetProperty("items").EnumerateArray());
        Assert.False(empty.GetProperty("hasMore").GetBoolean());
        Assert.Equal(HttpStatusCode.NotFound, unknown.StatusCode);
        Assert.Equal("application/problem+json", unknown.Content.Headers.ContentType?.MediaType);
    }

    [Theory]
    [InlineData("page=0", "page")]
    [InlineData("page=10001", "page")]
    [InlineData("page=abc", "page")]
    [InlineData("pageSize=0", "pageSize")]
    [InlineData("pageSize=101", "pageSize")]
    [InlineData("fromUtc=", "fromUtc")]
    [InlineData("fromUtc=2026-01-05", "fromUtc")]
    [InlineData("fromUtc=2026-01-05T08:02:00", "fromUtc")]
    [InlineData("toUtc=2026-01-05T08:03:00%2B01:00", "toUtc")]
    [InlineData("fromUtc=2026-01-05T08:03:00Z&toUtc=2026-01-05T08:02:00Z", "toUtc")]
    [InlineData("fromUtc=2026-01-05T08:03:00Z&toUtc=2026-01-05T08:03:00Z", "toUtc")]
    public async Task InvalidQueryReturnsValidationProblemDetails(string query, string key)
    {
        CancellationToken token = TestContext.Current.CancellationToken;
        using HttpResponseMessage response = await fixture.Client.GetAsync($"{RunningHistory}?{query}", token);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        JsonElement problem = await response.Content.ReadFromJsonAsync<JsonElement>(token);
        Assert.Contains(problem.GetProperty("errors").EnumerateObject(),
            error => error.Name.Equals(key, StringComparison.OrdinalIgnoreCase));
    }
}
