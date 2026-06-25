using FxRates.Core;

namespace FxRates.Tests.Fakes;

// Controllable clock so snapshot timestamps are deterministic in tests.
public class FakeClock : IClock
{
    public DateTimeOffset UtcNow { get; set; } = new(2026, 6, 25, 12, 0, 0, TimeSpan.Zero);

    public void Advance(TimeSpan by) => UtcNow = UtcNow.Add(by);
}
