using System.Text.Json;
using FxRates.Core;
using Microsoft.Extensions.Logging;

namespace FxRates.Infrastructure.Sources;

// fawazahmed0 currency-api (jsDelivr) — GET currencies/usd.json -> { "usd": { "php": 57.x, ... } }
public class CurrencyApiSource : IFxRateSource
{
    public const string SourceName = "fawazahmed0-currency-api";
    public string Name => SourceName;

    private readonly HttpClient _http;
    private readonly IClock _clock;
    private readonly ILogger<CurrencyApiSource> _logger;

    public CurrencyApiSource(HttpClient http, IClock clock, ILogger<CurrencyApiSource> logger)
    {
        _http = http;
        _clock = clock;
        _logger = logger;
    }

    public async Task<FxFetchResult> FetchAsync(CancellationToken cancellationToken)
    {
        try
        {
            using var stream = await _http.GetStreamAsync(
                "npm/@fawazahmed0/currency-api@latest/v1/currencies/usd.json", cancellationToken);
            using var doc = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);
            var rate = doc.RootElement.GetProperty("usd").GetProperty("php").GetDecimal();
            return FxFetchResult.Ok(Name, rate, _clock.UtcNow);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogWarning(ex, "{Source} fetch failed.", Name);
            return FxFetchResult.Failed(Name, _clock.UtcNow);
        }
    }
}
