using FxRates.Core;

namespace FxRates.Tests.Fakes;

// Scripted FX source for tests: returns a set rate, or a forced failure. No network.
public class FakeFxRateSource : IFxRateSource
{
    public string Name { get; }
    public decimal Rate { get; set; }
    public bool ShouldFail { get; set; }

    public FakeFxRateSource(string name, decimal rate)
    {
        Name = name;
        Rate = rate;
    }

    public Task<FxFetchResult> FetchAsync(CancellationToken cancellationToken)
    {
        var now = DateTimeOffset.UnixEpoch;
        return Task.FromResult(ShouldFail
            ? FxFetchResult.Failed(Name, now)
            : FxFetchResult.Ok(Name, Rate, now));
    }
}
