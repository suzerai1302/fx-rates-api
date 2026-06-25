using System.Collections.Concurrent;
using FxRates.Core;

namespace FxRates.Tests.Fakes;

// Captures outbound webhook deliveries instead of making network calls. Can be told
// to report failure to exercise the failed-delivery / loop-continues paths.
public class FakeWebhookSender : IWebhookSender
{
    public ConcurrentQueue<(string Url, WebhookPayload Payload)> Sends { get; } = new();
    public bool ShouldFail { get; set; }

    public Task<WebhookResult> SendAsync(string callbackUrl, WebhookPayload payload, CancellationToken cancellationToken)
    {
        Sends.Enqueue((callbackUrl, payload));
        return Task.FromResult(ShouldFail
            ? new WebhookResult(3, Success: false, LastError: "forced failure")
            : new WebhookResult(1, Success: true, LastError: null));
    }
}
