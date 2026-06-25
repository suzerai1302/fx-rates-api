namespace FxRates.Core;

// One FX rate provider (USD base, PHP quote). Each real source is an adapter in
// Infrastructure; tests supply a fake. Implementations never throw for an upstream
// failure — they return FxFetchResult.Failed so the refresh loop can fall back.
public interface IFxRateSource
{
    string Name { get; }

    Task<FxFetchResult> FetchAsync(CancellationToken cancellationToken);
}
