using FxRates.Core;
using FxRates.Infrastructure;
using FxRates.Tests.Fakes;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace FxRates.Tests;

// Boots the real app in the "Testing" environment (Program skips Postgres, the real
// HTTP sources, and the background loop there) and supplies a SQLite in-memory
// database, fake FX sources, and a controllable clock. Refreshes are driven
// explicitly via RefreshAsync() so tests are deterministic (no timing races).
public class TestWebApplicationFactory : WebApplicationFactory<Program>
{
    private readonly SqliteConnection _connection = new("DataSource=:memory:");

    // Default trio: median is 57. Tests may mutate rates / ShouldFail before RefreshAsync().
    public List<FakeFxRateSource> Sources { get; } = new()
    {
        new("source-a", 56m),
        new("source-b", 57m),
        new("source-c", 59m),
    };

    public FakeClock Clock { get; } = new();

    public FakeWebhookSender Webhooks { get; } = new();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        _connection.Open(); // keep the in-memory database alive for the factory's lifetime

        builder.ConfigureServices(services =>
        {
            services.AddDbContext<FxRatesDbContext>(options => options.UseSqlite(_connection));
            services.AddSingleton<IClock>(Clock);
            services.AddSingleton<IWebhookSender>(Webhooks);
            foreach (var source in Sources)
                services.AddSingleton<IFxRateSource>(source);

            using var scope = services.BuildServiceProvider().CreateScope();
            scope.ServiceProvider.GetRequiredService<FxRatesDbContext>().Database.EnsureCreated();
        });
    }

    // Runs one refresh cycle through the real RateRefresher (fetch fakes -> aggregate
    // -> persist -> cache), exactly as the background loop would.
    public Task RefreshAsync() =>
        Services.GetRequiredService<RateRefresher>().RefreshOnceAsync(CancellationToken.None);

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        if (disposing) _connection.Dispose();
    }
}
