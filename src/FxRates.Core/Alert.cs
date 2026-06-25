namespace FxRates.Core;

// A user's threshold alert on the latest USD->PHP median rate. Fires when that rate
// satisfies `Comparator Threshold`. Hysteresis state (slice #8) is IsArmed.
public class Alert
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid OwnerUserId { get; set; }

    public string Comparator { get; set; } = ">="; // ">=" | "<="
    public decimal Threshold { get; set; }
    public string CallbackUrl { get; set; } = string.Empty;

    public DateTime CreatedAt { get; set; }

    // True when ready to fire; set false after firing and re-armed when the
    // condition goes false again (slice #8). New alerts start armed.
    public bool IsArmed { get; set; } = true;
}
