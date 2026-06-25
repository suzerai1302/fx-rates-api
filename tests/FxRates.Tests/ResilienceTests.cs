using System.Net;
using System.Net.Http.Json;

namespace FxRates.Tests;

public class ResilienceTests
{
    [Fact]
    public async Task OneSourceDown_IsExcluded_AggregateFromSurvivors_StatusFailed()
    {
        await using var factory = new TestWebApplicationFactory();
        factory.Sources[0].ShouldFail = true; // source-a (56) fails; survivors 57, 59 -> median 58
        var client = factory.CreateClient();

        await factory.RefreshAsync();

        var body = await client.GetFromJsonAsync<RatesResponse>("/rates");
        Assert.NotNull(body);
        Assert.False(body!.Stale);
        Assert.Equal(58m, body.Aggregate.Median);
        Assert.Contains(body.Sources, s => s.Name == "source-a" && s.Status == "failed" && s.Rate == null);
        Assert.Equal(2, body.Sources.Count(s => s.Status == "ok"));
    }

    [Fact]
    public async Task AllSourcesDown_ServesLastGoodSnapshotStale_Never5xx()
    {
        await using var factory = new TestWebApplicationFactory();
        var client = factory.CreateClient();

        await factory.RefreshAsync(); // all ok -> median 57 cached

        foreach (var source in factory.Sources) source.ShouldFail = true;
        await factory.RefreshAsync(); // all fail -> should re-serve last good, marked stale

        var response = await client.GetAsync("/rates");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode); // never 5xx on upstream outage

        var body = await response.Content.ReadFromJsonAsync<RatesResponse>();
        Assert.NotNull(body);
        Assert.True(body!.Stale);
        Assert.Equal(57m, body.Aggregate.Median); // last good aggregate, unchanged
    }
}
