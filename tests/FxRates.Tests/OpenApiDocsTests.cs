namespace FxRates.Tests;

public class OpenApiDocsTests
{
    [Fact]
    public async Task OpenApiDocument_IsServed_WithBearerSecurityScheme()
    {
        await using var factory = new TestWebApplicationFactory();
        var client = factory.CreateClient();

        var response = await client.GetAsync("/openapi/v1.json");
        response.EnsureSuccessStatusCode();

        var json = await response.Content.ReadAsStringAsync();
        Assert.Contains("Bearer", json);     // security scheme documented
        Assert.Contains("/rates", json);     // endpoints present
    }
}
