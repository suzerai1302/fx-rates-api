using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using FxRates.API;
using FxRates.Core;
using FxRates.Infrastructure;
using FxRates.Infrastructure.Sources;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Scalar.AspNetCore;

var builder = WebApplication.CreateBuilder(args);

// Honor the platform-assigned port (Render sets PORT); ignored locally and under tests.
var port = Environment.GetEnvironmentVariable("PORT");
if (!string.IsNullOrEmpty(port))
    builder.WebHost.UseUrls($"http://0.0.0.0:{port}");

var isTesting = builder.Environment.IsEnvironment("Testing");

builder.Services.AddOpenApi(options =>
{
    // Document the JWT scheme + mark authed endpoints so Scalar shows an Authorize box.
    options.AddDocumentTransformer<BearerSecuritySchemeTransformer>();
    options.AddOperationTransformer<AuthorizationOperationTransformer>();
});

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
    builder.Services.AddHttpClient<IWebhookSender, HttpWebhookSender>(c => c.Timeout = TimeSpan.FromSeconds(10));

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

builder.Services.AddScoped<IUserRepository, EfUserRepository>();
builder.Services.AddScoped<IAlertRepository, EfAlertRepository>();
builder.Services.AddScoped<IAlertDeliveryRepository, EfAlertDeliveryRepository>();
builder.Services.AddSingleton<IPasswordHasher, BcryptPasswordHasher>();
builder.Services.AddSingleton<ITokenIssuer, JwtTokenIssuer>();

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = builder.Configuration["Jwt:Issuer"],
            ValidAudience = builder.Configuration["Jwt:Audience"],
            IssuerSigningKey = new SymmetricSecurityKey(
                Encoding.UTF8.GetBytes(builder.Configuration["Jwt:Key"]!)),
        };
    });
builder.Services.AddAuthorization();

var app = builder.Build();

// Render terminates TLS at a proxy and forwards plain HTTP with X-Forwarded-Proto.
// Honor it so Request.Scheme is "https" — otherwise the OpenAPI server URL is http://
// and Scalar's browser calls get blocked as mixed content. KnownProxies/Networks are
// cleared because the proxy isn't loopback.
var forwardedOptions = new ForwardedHeadersOptions
{
    ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto,
};
forwardedOptions.KnownIPNetworks.Clear();
forwardedOptions.KnownProxies.Clear();
app.UseForwardedHeaders(forwardedOptions);

// Apply pending migrations on startup (skipped under tests, which use SQLite).
if (!isTesting)
{
    using var scope = app.Services.CreateScope();
    scope.ServiceProvider.GetRequiredService<FxRatesDbContext>().Database.Migrate();
}

// OpenAPI spec + Scalar interactive docs, live in all environments for the demo.
app.MapOpenApi();
app.MapScalarApiReference();

app.UseAuthentication();
app.UseAuthorization();

app.MapGet("/health", () => Results.Ok(new { status = "ok" }));

app.MapPost("/auth/register", async (RegisterRequest request, IUserRepository users, IPasswordHasher hasher, CancellationToken ct) =>
{
    if (await users.GetByEmailAsync(request.Email, ct) is not null)
        return Results.Conflict(new { error = "Email already registered." });

    await users.AddAsync(new User { Email = request.Email, PasswordHash = hasher.Hash(request.Password) }, ct);
    return Results.Created($"/users/{request.Email}", null);
});

app.MapPost("/auth/login", async (LoginRequest request, IUserRepository users, IPasswordHasher hasher, ITokenIssuer tokens, CancellationToken ct) =>
{
    var user = await users.GetByEmailAsync(request.Email, ct);
    if (user is null || !hasher.Verify(request.Password, user.PasswordHash))
        return Results.Unauthorized();

    return Results.Ok(new { token = tokens.CreateToken(user) });
});

app.MapPost("/alerts", async (CreateAlertRequest request, IAlertRepository alerts, ClaimsPrincipal principal, IClock clock, CancellationToken ct) =>
{
    if (request.Comparator is not (">=" or "<="))
        return Results.Problem(statusCode: 400, title: "comparator must be \">=\" or \"<=\".");
    if (!Uri.TryCreate(request.CallbackUrl, UriKind.Absolute, out var uri) ||
        (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
        return Results.Problem(statusCode: 400, title: "callbackUrl must be an absolute http(s) URL.");

    var alert = new Alert
    {
        OwnerUserId = GetUserId(principal),
        Comparator = request.Comparator,
        Threshold = request.Threshold,
        CallbackUrl = request.CallbackUrl,
        CreatedAt = clock.UtcNow.UtcDateTime,
    };
    await alerts.AddAsync(alert, ct);
    return Results.Created($"/alerts/{alert.Id}",
        new AlertResponse(alert.Id, alert.Comparator, alert.Threshold, alert.CallbackUrl));
}).RequireAuthorization();

app.MapGet("/alerts", async (IAlertRepository alerts, ClaimsPrincipal principal, CancellationToken ct) =>
{
    var mine = await alerts.GetByOwnerAsync(GetUserId(principal), ct);
    return Results.Ok(mine.Select(a => new AlertResponse(a.Id, a.Comparator, a.Threshold, a.CallbackUrl)));
}).RequireAuthorization();

app.MapDelete("/alerts/{id:guid}", async (Guid id, IAlertRepository alerts, ClaimsPrincipal principal, CancellationToken ct) =>
{
    var deleted = await alerts.DeleteAsync(id, GetUserId(principal), ct);
    return deleted ? Results.NoContent() : Results.NotFound();
}).RequireAuthorization();

static Guid GetUserId(ClaimsPrincipal principal)
{
    var sub = principal.FindFirstValue(ClaimTypes.NameIdentifier)
              ?? principal.FindFirstValue(JwtRegisteredClaimNames.Sub);
    return Guid.Parse(sub!);
}

app.MapGet("/rates", (ILatestRateCache cache) =>
{
    var snapshot = cache.Current;
    return snapshot is null
        ? Results.Problem(statusCode: 503, title: "No rate snapshot available yet.")
        : Results.Ok(RatesResponse.From(snapshot));
});

app.MapGet("/convert", (decimal amount, string direction, ILatestRateCache cache) =>
{
    if (amount < 0)
        return Results.Problem(statusCode: 400, title: "amount must be non-negative.");

    var snapshot = cache.Current;
    if (snapshot is null)
        return Results.Problem(statusCode: 503, title: "No rate snapshot available yet.");

    var rate = snapshot.Median; // PHP per 1 USD
    decimal result;
    switch (direction)
    {
        case "USD_TO_PHP": result = amount * rate; break;
        case "PHP_TO_USD": result = amount / rate; break;
        default:
            return Results.Problem(statusCode: 400, title: "direction must be USD_TO_PHP or PHP_TO_USD.");
    }

    return Results.Ok(new ConvertResponse(amount, rate, Math.Round(result, 4), snapshot.AsOf));
});

app.MapGet("/rates/history", async (
    DateTime? from, DateTime? to, int? limit, IRateSnapshotRepository repository, CancellationToken ct) =>
{
    var take = limit ?? 100;
    if (take is <= 0 or > 1000)
        return Results.Problem(statusCode: 400, title: "limit must be between 1 and 1000.");
    if (from is not null && to is not null && from > to)
        return Results.Problem(statusCode: 400, title: "from must be on or before to.");

    var snapshots = await repository.GetHistoryAsync(from, to, take, ct);
    var points = snapshots
        .Select(s => new HistoryPoint(s.AsOf, s.Median, s.Mean, s.Min, s.Max))
        .ToList();
    return Results.Ok(points);
});

app.Run();

// Response DTOs for GET /rates.
public record AggregateDto(decimal Median, decimal Mean, decimal Min, decimal Max);
public record SourceDto(string Name, decimal? Rate, DateTime FetchedAt, string Status);
public record RatesResponse(
    DateTime AsOf, string Base, string Quote, bool Stale, AggregateDto Aggregate, IReadOnlyList<SourceDto> Sources)
{
    public static RatesResponse From(RateSnapshot s) => new(
        s.AsOf, s.Base, s.Quote, s.IsStale,
        new AggregateDto(s.Median, s.Mean, s.Min, s.Max),
        s.Sources.Select(sr => new SourceDto(sr.Name, sr.Rate, sr.FetchedAt, sr.Status)).ToList());
}

public record RegisterRequest(string Email, string Password);
public record LoginRequest(string Email, string Password);
public record CreateAlertRequest(string Comparator, decimal Threshold, string CallbackUrl);
public record AlertResponse(Guid Id, string Comparator, decimal Threshold, string CallbackUrl);
public record ConvertResponse(decimal Amount, decimal Rate, decimal Result, DateTime AsOf);
public record HistoryPoint(DateTime AsOf, decimal Median, decimal Mean, decimal Min, decimal Max);

// Exposed so the test project's WebApplicationFactory<Program> can boot the real app.
public partial class Program { }
