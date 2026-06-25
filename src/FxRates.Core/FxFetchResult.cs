namespace FxRates.Core;

// What a single FX source returns for one fetch attempt. Rate is null when the
// fetch failed; resilience/fallback (slice #3) decides what to do with failures.
public record FxFetchResult(string SourceName, decimal? Rate, DateTimeOffset FetchedAt, bool Success)
{
    public static FxFetchResult Ok(string name, decimal rate, DateTimeOffset fetchedAt) =>
        new(name, rate, fetchedAt, true);

    public static FxFetchResult Failed(string name, DateTimeOffset fetchedAt) =>
        new(name, null, fetchedAt, false);
}
