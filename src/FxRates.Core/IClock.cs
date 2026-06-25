namespace FxRates.Core;

// Abstraction over "now" so refresh/alert logic is deterministic in tests.
public interface IClock
{
    DateTimeOffset UtcNow { get; }
}
