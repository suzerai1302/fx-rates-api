using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;

namespace FxRates.Tests;

public class AlertsEndpointTests
{
    private record TokenResponse(string Token);
    private record AlertResponse(Guid Id, string Comparator, decimal Threshold, string CallbackUrl);

    private static async Task<HttpClient> AuthedClientAsync(TestWebApplicationFactory factory, string email)
    {
        var client = factory.CreateClient();
        var creds = new { email, password = "pw123456" };
        await client.PostAsJsonAsync("/auth/register", creds);
        var login = await client.PostAsJsonAsync("/auth/login", creds);
        var token = (await login.Content.ReadFromJsonAsync<TokenResponse>())!.Token;
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return client;
    }

    private static object Alert(string comparator = ">=", decimal threshold = 60m) =>
        new { comparator, threshold, callbackUrl = "https://example.com/hook" };

    [Fact]
    public async Task Alerts_WithoutToken_Returns401()
    {
        await using var factory = new TestWebApplicationFactory();
        var client = factory.CreateClient();

        var response = await client.GetAsync("/alerts");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task CreateAlert_ThenList_ReturnsIt()
    {
        await using var factory = new TestWebApplicationFactory();
        var client = await AuthedClientAsync(factory, "owner@example.com");

        var created = await client.PostAsJsonAsync("/alerts", Alert());
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);

        var alerts = await client.GetFromJsonAsync<AlertResponse[]>("/alerts");
        Assert.Single(alerts!);
        Assert.Equal(60m, alerts![0].Threshold);
        Assert.Equal(">=", alerts[0].Comparator);
    }

    [Fact]
    public async Task List_ReturnsOnlyCallersAlerts()
    {
        await using var factory = new TestWebApplicationFactory();
        var alice = await AuthedClientAsync(factory, "alice@example.com");
        var bob = await AuthedClientAsync(factory, "bob@example.com");
        await alice.PostAsJsonAsync("/alerts", Alert());

        var bobsAlerts = await bob.GetFromJsonAsync<AlertResponse[]>("/alerts");

        Assert.Empty(bobsAlerts!);
    }

    [Fact]
    public async Task Delete_RemovesCallersAlert()
    {
        await using var factory = new TestWebApplicationFactory();
        var client = await AuthedClientAsync(factory, "del@example.com");
        var created = await client.PostAsJsonAsync("/alerts", Alert());
        var alert = await created.Content.ReadFromJsonAsync<AlertResponse>();

        var delete = await client.DeleteAsync($"/alerts/{alert!.Id}");
        Assert.Equal(HttpStatusCode.NoContent, delete.StatusCode);

        var remaining = await client.GetFromJsonAsync<AlertResponse[]>("/alerts");
        Assert.Empty(remaining!);
    }

    [Fact]
    public async Task Delete_CannotRemoveAnotherUsersAlert()
    {
        await using var factory = new TestWebApplicationFactory();
        var alice = await AuthedClientAsync(factory, "alice2@example.com");
        var bob = await AuthedClientAsync(factory, "bob2@example.com");
        var created = await alice.PostAsJsonAsync("/alerts", Alert());
        var alert = await created.Content.ReadFromJsonAsync<AlertResponse>();

        var bobDelete = await bob.DeleteAsync($"/alerts/{alert!.Id}");

        Assert.Equal(HttpStatusCode.NotFound, bobDelete.StatusCode);
    }
}
