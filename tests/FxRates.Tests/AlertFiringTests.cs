using System.Net.Http.Headers;
using System.Net.Http.Json;
using FxRates.Core;
using Microsoft.Extensions.DependencyInjection;

namespace FxRates.Tests;

public class AlertFiringTests
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

    private static void SetAllRates(TestWebApplicationFactory factory, decimal rate)
    {
        foreach (var source in factory.Sources) source.Rate = rate;
    }

    [Fact]
    public async Task RefreshCrossingThreshold_FiresWebhookOnce_ThenReArmsAndFiresAgain()
    {
        await using var factory = new TestWebApplicationFactory(); // median 57
        var client = await AuthedClientAsync(factory, "fire@example.com");
        await client.PostAsJsonAsync("/alerts",
            new { comparator = ">=", threshold = 56.5m, callbackUrl = "https://example.com/hook" });

        await factory.RefreshAsync();                       // 57 >= 56.5 -> fire
        Assert.Equal(1, factory.Webhooks.Sends.Count);

        await factory.RefreshAsync();                       // still crossed -> no re-fire (hysteresis)
        Assert.Equal(1, factory.Webhooks.Sends.Count);

        SetAllRates(factory, 50m);
        await factory.RefreshAsync();                       // condition false -> re-arm, no fire
        Assert.Equal(1, factory.Webhooks.Sends.Count);

        SetAllRates(factory, 57m);
        await factory.RefreshAsync();                       // crosses again -> fires
        Assert.Equal(2, factory.Webhooks.Sends.Count);
    }

    [Fact]
    public async Task FailingWebhook_RecordsFailedDelivery_AndDoesNotBreakLoop()
    {
        await using var factory = new TestWebApplicationFactory();
        factory.Webhooks.ShouldFail = true;
        var client = await AuthedClientAsync(factory, "failhook@example.com");
        var created = await client.PostAsJsonAsync("/alerts",
            new { comparator = ">=", threshold = 56.5m, callbackUrl = "https://example.com/hook" });
        var alert = await created.Content.ReadFromJsonAsync<AlertResponse>();

        await factory.RefreshAsync(); // fires, delivery fails (no throw)

        Assert.Equal(1, factory.Webhooks.Sends.Count);

        using var scope = factory.Services.CreateScope();
        var deliveries = await scope.ServiceProvider
            .GetRequiredService<IAlertDeliveryRepository>()
            .GetByAlertAsync(alert!.Id, CancellationToken.None);
        Assert.Single(deliveries);
        Assert.Equal("failed", deliveries[0].Status);
    }
}
