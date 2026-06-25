using System.Net;
using System.Net.Http.Json;

namespace FxRates.Tests;

public class RatesEndpointTests
{
    [Fact]
    public async Task Rates_ReturnsMedianAggregate_AndPerSourceStatus()
    {
        await using var factory = new TestWebApplicationFactory();
        var client = factory.CreateClient();

        await factory.RefreshAsync(); // sources 56, 57, 59 -> median 57

        var response = await client.GetAsync("/rates");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<RatesResponse>();
        Assert.NotNull(body);
        Assert.Equal("USD", body!.Base);
        Assert.Equal("PHP", body.Quote);
        Assert.False(body.Stale);
        Assert.Equal(57m, body.Aggregate.Median);
        Assert.Equal(56m, body.Aggregate.Min);
        Assert.Equal(59m, body.Aggregate.Max);

        Assert.Equal(3, body.Sources.Count);
        Assert.All(body.Sources, s => Assert.Equal("ok", s.Status));
        Assert.Contains(body.Sources, s => s.Name == "source-b" && s.Rate == 57m);
    }

    [Fact]
    public async Task Rates_BeforeAnyRefresh_Returns503()
    {
        await using var factory = new TestWebApplicationFactory();
        var client = factory.CreateClient();

        var response = await client.GetAsync("/rates");

        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
    }
}
