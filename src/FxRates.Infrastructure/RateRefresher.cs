using FxRates.Core;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace FxRates.Infrastructure;

// Orchestrates one refresh: fetch all sources concurrently, aggregate the
// survivors, persist a snapshot, update the latest-rate cache. Kept separate from
// the hosted BackgroundService so tests can drive a refresh deterministically.
public class RateRefresher
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILatestRateCache _cache;
    private readonly IClock _clock;
    private readonly ILogger<RateRefresher> _logger;

    public RateRefresher(
        IServiceScopeFactory scopeFactory,
        ILatestRateCache cache,
        IClock clock,
        ILogger<RateRefresher> logger)
    {
        _scopeFactory = scopeFactory;
        _cache = cache;
        _clock = clock;
        _logger = logger;
    }

    public async Task RefreshOnceAsync(CancellationToken cancellationToken)
    {
        using var scope = _scopeFactory.CreateScope();
        var sources = scope.ServiceProvider.GetServices<IFxRateSource>().ToList();
        var repository = scope.ServiceProvider.GetRequiredService<IRateSnapshotRepository>();

        var results = await Task.WhenAll(sources.Select(s => s.FetchAsync(cancellationToken)));

        var survivors = results.Where(r => r is { Success: true, Rate: not null })
            .Select(r => r.Rate!.Value)
            .ToList();
        var sourceRates = results.Select(SourceRate.From).ToList();

        if (survivors.Count == 0)
        {
            // Every source failed. Re-serve the last good snapshot marked stale
            // (AsOf unchanged) so reads never 5xx on an upstream outage. With no
            // prior snapshot there is simply nothing to serve yet.
            if (_cache.Current is { } previous)
            {
                previous.IsStale = true;
                _cache.Set(previous);
            }
            _logger.LogWarning("All FX sources failed this refresh; serving last good snapshot as stale.");
            return;
        }

        var aggregate = RateAggregator.Aggregate(survivors);
        var snapshot = RateSnapshot.FromAggregate(aggregate, _clock.UtcNow, sourceRates);

        await repository.AddAsync(snapshot, cancellationToken);
        _cache.Set(snapshot);

        _logger.LogInformation(
            "Refreshed FX rates: median {Median} from {Ok}/{Total} sources.",
            aggregate.Median, survivors.Count, sources.Count);
    }
}
