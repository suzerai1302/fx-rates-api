using FxRates.Core;

namespace FxRates.Infrastructure;

// Singleton holding the most recent snapshot. Reads are lock-free; writes just
// swap the reference (a single refresh loop is the only writer).
public class InMemoryLatestRateCache : ILatestRateCache
{
    private volatile RateSnapshot? _current;

    public RateSnapshot? Current => _current;

    public void Set(RateSnapshot snapshot) => _current = snapshot;
}
