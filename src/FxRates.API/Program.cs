using FxRates.Core;
using FxRates.Infrastructure;
using FxRates.Infrastructure.Sources;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

// Honor the platform-assigned port (Render sets PORT); ignored locally and under tests.
var port = Environment.GetEnvironmentVariable("PORT");
if (!string.IsNullOrEmpty(port))
    builder.WebHost.UseUrls($"http://0.0.0.0:{port}");

var isTesting = builder.Environment.IsEnvironment("Testing");

// In the Testing environment the test host supplies the DbContext (SQLite) and a
// fake FX source, and drives refreshes manually — so we skip Postgres, the real
// HTTP sources, and the background loop here.
if (!isTesting)
{
    var pgConn = builder.Configuration.GetConnectionString("Postgres");
    var databaseUrl = Environment.GetEnvironmentVariable("DATABASE_URL");
    if (!string.IsNullOrEmpty(databaseUrl))
    {
        // Render/Neon expose a postgres:// URL; convert it to an Npgsql connection string.
        var uri = new Uri(databaseUrl);
        var creds = uri.UserInfo.Split(':', 2);
        var dbPort = uri.Port == -1 ? 5432 : uri.Port;
        var user = Uri.UnescapeDataString(creds[0]);
        var pass = creds.Length > 1 ? Uri.UnescapeDataString(creds[1]) : "";
        pgConn = $"Host={uri.Host};Port={dbPort};Database={uri.AbsolutePath.TrimStart('/')};" +
                 $"Username={user};Password={pass};SSL Mode=Require;Trust Server Certificate=true";
    }

    builder.Services.AddDbContext<FxRatesDbContext>(options => options.UseNpgsql(pgConn));

    builder.Services.AddFxSources();

    var intervalMinutes = builder.Configuration.GetValue("Fx:RefreshIntervalMinutes", 10);
    builder.Services.AddSingleton<IHostedService>(sp => new RateRefreshService(
        sp.GetRequiredService<RateRefresher>(),
        sp.GetRequiredService<ILogger<RateRefreshService>>(),
        TimeSpan.FromMinutes(intervalMinutes)));
}

builder.Services.AddScoped<IRateSnapshotRepository, EfRateSnapshotRepository>();
builder.Services.AddSingleton<IClock, SystemClock>();
builder.Services.AddSingleton<ILatestRateCache, InMemoryLatestRateCache>();
builder.Services.AddSingleton<RateRefresher>();

var app = builder.Build();

// Apply pending migrations on startup (skipped under tests, which use SQLite).
if (!isTesting)
{
    using var scope = app.Services.CreateScope();
    scope.ServiceProvider.GetRequiredService<FxRatesDbContext>().Database.Migrate();
}

app.MapGet("/health", () => Results.Ok(new { status = "ok" }));

app.MapGet("/rates", (ILatestRateCache cache) =>
{
    var snapshot = cache.Current;
    return snapshot is null
        ? Results.Problem(statusCode: 503, title: "No rate snapshot available yet.")
        : Results.Ok(RatesResponse.From(snapshot));
});

app.Run();

// Response DTOs for GET /rates.
public record AggregateDto(decimal Median, decimal Mean, decimal Min, decimal Max);
public record SourceDto(string Name, decimal? Rate, DateTimeOffset FetchedAt, string Status);
public record RatesResponse(
    DateTimeOffset AsOf, string Base, string Quote, bool Stale, AggregateDto Aggregate, IReadOnlyList<SourceDto> Sources)
{
    public static RatesResponse From(RateSnapshot s) => new(
        s.AsOf, s.Base, s.Quote, s.IsStale,
        new AggregateDto(s.Median, s.Mean, s.Min, s.Max),
        s.Sources.Select(sr => new SourceDto(sr.Name, sr.Rate, sr.FetchedAt, sr.Status)).ToList());
}

// Exposed so the test project's WebApplicationFactory<Program> can boot the real app.
public partial class Program { }
