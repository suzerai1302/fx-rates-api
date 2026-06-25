namespace FxRates.Core;

public record AlertEvaluation(bool ShouldFire, bool IsArmed);

// Pure hysteresis logic. An alert fires once when its condition becomes true, then
// stays disarmed (no re-firing every refresh while still crossed) until the
// condition goes false again, which re-arms it.
public static class AlertEvaluator
{
    public static AlertEvaluation Evaluate(string comparator, decimal threshold, bool isArmed, decimal rate)
    {
        var conditionMet = comparator switch
        {
            ">=" => rate >= threshold,
            "<=" => rate <= threshold,
            _ => throw new ArgumentException($"Unknown comparator '{comparator}'.", nameof(comparator)),
        };

        if (!conditionMet)
            return new AlertEvaluation(ShouldFire: false, IsArmed: true);   // re-arm
        if (isArmed)
            return new AlertEvaluation(ShouldFire: true, IsArmed: false);   // fire once, disarm
        return new AlertEvaluation(ShouldFire: false, IsArmed: false);      // still crossed, no refire
    }
}
