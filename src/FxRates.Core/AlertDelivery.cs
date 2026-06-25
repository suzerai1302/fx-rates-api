namespace FxRates.Core;

// Record of one webhook delivery attempt-set for a fired alert.
public class AlertDelivery
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid AlertId { get; set; }
    public DateTime FiredAt { get; set; }
    public decimal Rate { get; set; }
    public string Status { get; set; } = "delivered"; // "delivered" | "failed"
    public int Attempts { get; set; }
    public string? LastError { get; set; }
}
