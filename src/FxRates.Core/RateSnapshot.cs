namespace FxRates.Core;

// A persisted aggregation result for one refresh. The sequence of snapshots IS
// the rate history (slice #5 queries it). Base/Quote are USD/PHP in v1.
public class RateSnapshot
{
    public int Id { get; set; }
    public DateTime AsOf { get; set; } // UTC instant; DateTime (not DateTimeOffset) so SQLite can sort/filter it
    public string Base { get; set; } = "USD";
    public string Quote { get; set; } = "PHP";

    public decimal Median { get; set; }
    public decimal Mean { get; set; }
    public decimal Min { get; set; }
    public decimal Max { get; set; }

    // True when this snapshot is being re-served because every source failed on a
    // later refresh (slice #3). The stored aggregate is unchanged; AsOf is the
    // original fetch time.
    public bool IsStale { get; set; }

    public List<SourceRate> Sources { get; set; } = new();

    public static RateSnapshot FromAggregate(RateAggregate aggregate, DateTime asOf, IEnumerable<SourceRate> sources) =>
        new()
        {
            AsOf = asOf,
            Median = aggregate.Median,
            Mean = aggregate.Mean,
            Min = aggregate.Min,
            Max = aggregate.Max,
            Sources = sources.ToList(),
        };
}
