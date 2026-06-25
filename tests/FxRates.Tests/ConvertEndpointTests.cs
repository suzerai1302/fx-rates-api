using System.Net;
using System.Net.Http.Json;

namespace FxRates.Tests;

public class ConvertEndpointTests
{
    private static async Task<TestWebApplicationFactory> ReadyAsync()
    {
        var factory = new TestWebApplicationFactory(); // sources 56/57/59 -> median 57
        await factory.RefreshAsync();
        return factory;
    }

    [Fact]
    public async Task Convert_UsdToPhp_MultipliesByMedian()
    {
        await using var factory = await ReadyAsync();
        var client = factory.CreateClient();

        var body = await client.GetFromJsonAsync<ConvertResponse>("/convert?amount=10&direction=USD_TO_PHP");

        Assert.NotNull(body);
        Assert.Equal(10m, body!.Amount);
        Assert.Equal(57m, body.Rate);
        Assert.Equal(570m, body.Result);
    }

    [Fact]
    public async Task Convert_PhpToUsd_DividesByMedian()
    {
        await using var factory = await ReadyAsync();
        var client = factory.CreateClient();

        var body = await client.GetFromJsonAsync<ConvertResponse>("/convert?amount=570&direction=PHP_TO_USD");

        Assert.NotNull(body);
        Assert.Equal(10m, body!.Result);
    }

    [Fact]
    public async Task Convert_UnknownDirection_Returns400()
    {
        await using var factory = await ReadyAsync();
        var client = factory.CreateClient();

        var response = await client.GetAsync("/convert?amount=10&direction=USD_TO_EUR");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Convert_NegativeAmount_Returns400()
    {
        await using var factory = await ReadyAsync();
        var client = factory.CreateClient();

        var response = await client.GetAsync("/convert?amount=-5&direction=USD_TO_PHP");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }
}
