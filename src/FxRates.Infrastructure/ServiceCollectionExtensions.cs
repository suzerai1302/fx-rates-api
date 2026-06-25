using FxRates.Core;
using FxRates.Infrastructure.Sources;
using Microsoft.Extensions.DependencyInjection;

namespace FxRates.Infrastructure;

public static class ServiceCollectionExtensions
{
    // Registers the real keyless FX sources as configured typed HttpClients and
    // exposes each as an IFxRateSource so the refresher gets all of them via
    // GetServices<IFxRateSource>(). Not called under tests (a fake is used instead).
    public static IServiceCollection AddFxSources(this IServiceCollection services)
    {
        // Each source gets timeout + retry-with-backoff + circuit breaker so a slow or
        // flaky upstream is bounded; the refresher then drops whatever still failed.
        services.AddHttpClient<OpenErApiSource>(c => c.BaseAddress = new Uri("https://open.er-api.com/"))
            .AddStandardResilienceHandler();
        services.AddHttpClient<FloatRatesSource>(c => c.BaseAddress = new Uri("https://www.floatrates.com/"))
            .AddStandardResilienceHandler();
        services.AddHttpClient<CurrencyApiSource>(c => c.BaseAddress = new Uri("https://cdn.jsdelivr.net/"))
            .AddStandardResilienceHandler();

        services.AddTransient<IFxRateSource>(sp => sp.GetRequiredService<OpenErApiSource>());
        services.AddTransient<IFxRateSource>(sp => sp.GetRequiredService<FloatRatesSource>());
        services.AddTransient<IFxRateSource>(sp => sp.GetRequiredService<CurrencyApiSource>());

        return services;
    }
}
