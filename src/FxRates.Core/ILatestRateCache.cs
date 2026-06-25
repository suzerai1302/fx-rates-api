namespace FxRates.Core;

// In-memory cache of the latest snapshot so reads (/rates, /convert) are fast and
// never hit upstream inline. Populated by the refresh loop.
public interface ILatestRateCache
{
    RateSnapshot? Current { get; }
    void Set(RateSnapshot snapshot);
}
