namespace FxRates.Core;

// One source's contribution to a snapshot, including whether it succeeded — this
// is what surfaces as the per-source `status` in GET /rates.
public class SourceRate
{
    public int Id { get; set; }
    public int RateSnapshotId { get; set; }

    public string Name { get; set; } = string.Empty;
    public decimal? Rate { get; set; }
    public DateTimeOffset FetchedAt { get; set; }
    public string Status { get; set; } = "ok"; // "ok" | "failed"

    public static SourceRate From(FxFetchResult result) =>
        new()
        {
            Name = result.SourceName,
            Rate = result.Rate,
            FetchedAt = result.FetchedAt,
            Status = result.Success ? "ok" : "failed",
        };
}
