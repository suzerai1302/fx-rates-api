using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace FxRates.Infrastructure;

// Hosted loop that refreshes on startup and then every RefreshInterval. A failing
// refresh is logged and the loop continues — one bad tick never kills the service.
public class RateRefreshService : BackgroundService
{
    private readonly RateRefresher _refresher;
    private readonly ILogger<RateRefreshService> _logger;
    private readonly TimeSpan _interval;

    public RateRefreshService(RateRefresher refresher, ILogger<RateRefreshService> logger, TimeSpan interval)
    {
        _refresher = refresher;
        _logger = logger;
        _interval = interval;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await RunSafelyAsync(stoppingToken);

        using var timer = new PeriodicTimer(_interval);
        while (await timer.WaitForNextTickAsync(stoppingToken))
            await RunSafelyAsync(stoppingToken);
    }

    private async Task RunSafelyAsync(CancellationToken cancellationToken)
    {
        try
        {
            await _refresher.RefreshOnceAsync(cancellationToken);
        }
        catch (OperationCanceledException)
        {
            // shutting down — let it propagate out of the loop via the token
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "FX rate refresh failed; will retry next interval.");
        }
    }
}
