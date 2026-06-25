using System.Text.Json;
using FxRates.Core;
using Microsoft.Extensions.Logging;

namespace FxRates.Infrastructure.Sources;

// open.er-api.com — GET /v6/latest/USD -> { "rates": { "PHP": 57.x, ... } }
public class OpenErApiSource : IFxRateSource
{
    public const string SourceName = "open.er-api.com";
    public string Name => SourceName;

    private readonly HttpClient _http;
    private readonly IClock _clock;
    private readonly ILogger<OpenErApiSource> _logger;

    public OpenErApiSource(HttpClient http, IClock clock, ILogger<OpenErApiSource> logger)
    {
        _http = http;
        _clock = clock;
        _logger = logger;
    }

    public async Task<FxFetchResult> FetchAsync(CancellationToken cancellationToken)
    {
        try
        {
            using var stream = await _http.GetStreamAsync("v6/latest/USD", cancellationToken);
            using var doc = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);
            var rate = doc.RootElement.GetProperty("rates").GetProperty("PHP").GetDecimal();
            return FxFetchResult.Ok(Name, rate, _clock.UtcNow);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogWarning(ex, "{Source} fetch failed.", Name);
            return FxFetchResult.Failed(Name, _clock.UtcNow);
        }
    }
}
