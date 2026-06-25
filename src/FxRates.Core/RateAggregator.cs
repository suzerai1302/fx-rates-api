namespace FxRates.Core;

// Pure aggregation over the surviving source rates. No I/O — the equivalent of
// receipts-api's SettlementCalculator. Callers pass only the rates they trust
// (failed sources are excluded upstream); resilience/fallback lives elsewhere.
public static class RateAggregator
{
    public static RateAggregate Aggregate(IReadOnlyCollection<decimal> rates)
    {
        if (rates.Count == 0)
            throw new ArgumentException("Cannot aggregate an empty set of rates.", nameof(rates));

        var sorted = rates.OrderBy(r => r).ToList();
        var n = sorted.Count;
        var median = n % 2 == 1
            ? sorted[n / 2]
            : (sorted[n / 2 - 1] + sorted[n / 2]) / 2m;

        return new RateAggregate(
            Median: median,
            Mean: sorted.Sum() / n,
            Min: sorted[0],
            Max: sorted[n - 1]);
    }
}
