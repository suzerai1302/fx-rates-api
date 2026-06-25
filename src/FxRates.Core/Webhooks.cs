namespace FxRates.Core;

// Body POSTed to an alert's callback URL when it fires.
public record WebhookPayload(Guid AlertId, string Comparator, decimal Threshold, decimal Rate, DateTime AsOf);

// Outcome of an outbound webhook delivery (after any retries).
public record WebhookResult(int Attempts, bool Success, string? LastError);

public interface IWebhookSender
{
    Task<WebhookResult> SendAsync(string callbackUrl, WebhookPayload payload, CancellationToken cancellationToken);
}
