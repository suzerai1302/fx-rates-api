using System.Net.Http.Json;
using FxRates.Core;
using Microsoft.Extensions.Logging;

namespace FxRates.Infrastructure;

// Outbound webhook delivery with bounded exponential-backoff retry. Never throws on
// a delivery failure — returns a WebhookResult the caller records as an AlertDelivery.
public class HttpWebhookSender : IWebhookSender
{
    private const int MaxAttempts = 3;

    private readonly HttpClient _http;
    private readonly ILogger<HttpWebhookSender> _logger;

    public HttpWebhookSender(HttpClient http, ILogger<HttpWebhookSender> logger)
    {
        _http = http;
        _logger = logger;
    }

    public async Task<WebhookResult> SendAsync(string callbackUrl, WebhookPayload payload, CancellationToken cancellationToken)
    {
        string? lastError = null;
        for (var attempt = 1; attempt <= MaxAttempts; attempt++)
        {
            try
            {
                var response = await _http.PostAsJsonAsync(callbackUrl, payload, cancellationToken);
                if (response.IsSuccessStatusCode)
                    return new WebhookResult(attempt, Success: true, LastError: null);

                lastError = $"HTTP {(int)response.StatusCode}";
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                lastError = ex.Message;
            }

            if (attempt < MaxAttempts)
                await Task.Delay(TimeSpan.FromMilliseconds(200 * Math.Pow(2, attempt - 1)), cancellationToken);
        }

        _logger.LogWarning("Webhook to {Url} failed after {Attempts} attempts: {Error}",
            callbackUrl, MaxAttempts, lastError);
        return new WebhookResult(MaxAttempts, Success: false, lastError);
    }
}
