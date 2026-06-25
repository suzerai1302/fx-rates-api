using System.Net;
using System.Net.Http.Json;

namespace FxRates.Tests;

public class HistoryEndpointTests
{
    private static async Task<TestWebApplicationFactory> SeedThreeSnapshotsAsync()
    {
        var factory = new TestWebApplicationFactory();
        await factory.RefreshAsync();                          // t0
        factory.Clock.Advance(TimeSpan.FromMinutes(1));
        await factory.RefreshAsync();                          // t0 + 1m
        factory.Clock.Advance(TimeSpan.FromMinutes(1));
        await factory.RefreshAsync();                          // t0 + 2m
        return factory;
    }

    [Fact]
    public async Task History_ReturnsPoints_NewestFirst()
    {
        await using var factory = await SeedThreeSnapshotsAsync();
        var client = factory.CreateClient();

        var points = await client.GetFromJsonAsync<HistoryPoint[]>("/rates/history");

        Assert.NotNull(points);
        Assert.Equal(3, points!.Length);
        Assert.True(points[0].AsOf > points[1].AsOf);
        Assert.True(points[1].AsOf > points[2].AsOf);
        Assert.Equal(57m, points[0].Median);
    }

    [Fact]
    public async Task History_LimitCapsCount()
    {
        await using var factory = await SeedThreeSnapshotsAsync();
        var client = factory.CreateClient();

        var points = await client.GetFromJsonAsync<HistoryPoint[]>("/rates/history?limit=2");

        Assert.Equal(2, points!.Length);
    }

    [Fact]
    public async Task History_FromFilter_ExcludesEarlierSnapshots()
    {
        await using var factory = new TestWebApplicationFactory();
        var start = factory.Clock.UtcNow;
        await factory.RefreshAsync();                          // t0 = start
        factory.Clock.Advance(TimeSpan.FromMinutes(1));
        await factory.RefreshAsync();                          // t0 + 1m
        factory.Clock.Advance(TimeSpan.FromMinutes(1));
        await factory.RefreshAsync();                          // t0 + 2m
        var client = factory.CreateClient();

        // Wall-clock UTC without a zone designator: bound and stored values then
        // compare by identical ticks regardless of DateTime.Kind.
        var from = start.UtcDateTime.AddSeconds(30).ToString("s");
        var points = await client.GetFromJsonAsync<HistoryPoint[]>($"/rates/history?from={from}");

        Assert.Equal(2, points!.Length); // only the t0+1m and t0+2m snapshots
    }

    [Fact]
    public async Task History_BadLimit_Returns400()
    {
        await using var factory = await SeedThreeSnapshotsAsync();
        var client = factory.CreateClient();

        var response = await client.GetAsync("/rates/history?limit=0");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }
}
