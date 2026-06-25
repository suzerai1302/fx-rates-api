namespace FxRates.Core;

// The aggregated view across all surviving source rates for one refresh.
public record RateAggregate(decimal Median, decimal Mean, decimal Min, decimal Max);
