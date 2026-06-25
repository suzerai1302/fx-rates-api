using FxRates.Core;

namespace FxRates.Tests;

public class AlertEvaluatorTests
{
    [Fact]
    public void Gte_ConditionBecomesTrue_WhileArmed_Fires_AndDisarms()
    {
        var result = AlertEvaluator.Evaluate(">=", 56.5m, isArmed: true, rate: 57m);

        Assert.True(result.ShouldFire);
        Assert.False(result.IsArmed);
    }

    [Fact]
    public void Gte_StillCrossed_WhileDisarmed_DoesNotRefire()
    {
        var result = AlertEvaluator.Evaluate(">=", 56.5m, isArmed: false, rate: 57m);

        Assert.False(result.ShouldFire);
        Assert.False(result.IsArmed);
    }

    [Fact]
    public void ConditionGoesFalse_ReArms_WithoutFiring()
    {
        var result = AlertEvaluator.Evaluate(">=", 56.5m, isArmed: false, rate: 50m);

        Assert.False(result.ShouldFire);
        Assert.True(result.IsArmed);
    }

    [Fact]
    public void Gte_NotCrossed_WhileArmed_DoesNotFire_StaysArmed()
    {
        var result = AlertEvaluator.Evaluate(">=", 56.5m, isArmed: true, rate: 50m);

        Assert.False(result.ShouldFire);
        Assert.True(result.IsArmed);
    }

    [Fact]
    public void Lte_ConditionBecomesTrue_WhileArmed_Fires()
    {
        var result = AlertEvaluator.Evaluate("<=", 56.5m, isArmed: true, rate: 50m);

        Assert.True(result.ShouldFire);
        Assert.False(result.IsArmed);
    }
}
