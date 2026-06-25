using System.Text.Json;
using FxRates.Core;
using Microsoft.Extensions.Logging;

namespace FxRates.Infrastructure.Sources;

// floatrates.com — GET /daily/usd.json -> { "php": { "rate": 57.x, ... }, ... }
public class FloatRatesSource : IFxRateSource
{
    public const string SourceName = "floatrates.com";
    public string Name => SourceName;

    private readonly HttpClient _http;
    private readonly IClock _clock;
    private readonly ILogger<FloatRatesSource> _logger;

    public FloatRatesSource(HttpClient http, IClock clock, ILogger<FloatRatesSource> logger)
    {
        _http = http;
        _clock = clock;
        _logger = logger;
    }

    public async Task<FxFetchResult> FetchAsync(CancellationToken cancellationToken)
    {
        try
        {
            using var stream = await _http.GetStreamAsync("daily/usd.json", cancellationToken);
            using var doc = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);
            var rate = doc.RootElement.GetProperty("php").GetProperty("rate").GetDecimal();
            return FxFetchResult.Ok(Name, rate, _clock.UtcNow);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogWarning(ex, "{Source} fetch failed.", Name);
            return FxFetchResult.Failed(Name, _clock.UtcNow);
        }
    }
}
