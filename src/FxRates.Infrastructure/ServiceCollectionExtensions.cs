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
        services.AddHttpClient<OpenErApiSource>(c => c.BaseAddress = new Uri("https://open.er-api.com/"));
        services.AddHttpClient<FloatRatesSource>(c => c.BaseAddress = new Uri("https://www.floatrates.com/"));
        services.AddHttpClient<CurrencyApiSource>(c => c.BaseAddress = new Uri("https://cdn.jsdelivr.net/"));

        services.AddTransient<IFxRateSource>(sp => sp.GetRequiredService<OpenErApiSource>());
        services.AddTransient<IFxRateSource>(sp => sp.GetRequiredService<FloatRatesSource>());
        services.AddTransient<IFxRateSource>(sp => sp.GetRequiredService<CurrencyApiSource>());

        return services;
    }
}
