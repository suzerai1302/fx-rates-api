using FxRates.Core;

namespace FxRates.Tests;

public class RateAggregatorTests
{
    [Fact]
    public void OddCount_MedianIsMiddleValue_PlusMeanMinMax()
    {
        // 56, 57, 59 -> sorted middle is 57; mean = 57.333...; min 56; max 59
        var result = RateAggregator.Aggregate(new[] { 57m, 56m, 59m });

        Assert.Equal(57m, result.Median);
        Assert.Equal(56m, result.Min);
        Assert.Equal(59m, result.Max);
        Assert.Equal(Math.Round((57m + 56m + 59m) / 3m, 6), Math.Round(result.Mean, 6));
    }

    [Fact]
    public void EvenCount_MedianIsAverageOfTwoMiddle()
    {
        // 56, 57, 58, 59 -> two middle 57,58 -> median 57.5
        var result = RateAggregator.Aggregate(new[] { 59m, 56m, 58m, 57m });

        Assert.Equal(57.5m, result.Median);
        Assert.Equal(56m, result.Min);
        Assert.Equal(59m, result.Max);
    }

    [Fact]
    public void SingleSurvivor_AllStatsEqualThatRate()
    {
        var result = RateAggregator.Aggregate(new[] { 56.5m });

        Assert.Equal(56.5m, result.Median);
        Assert.Equal(56.5m, result.Mean);
        Assert.Equal(56.5m, result.Min);
        Assert.Equal(56.5m, result.Max);
    }

    [Fact]
    public void EmptyInput_Throws()
    {
        Assert.Throws<ArgumentException>(() => RateAggregator.Aggregate(Array.Empty<decimal>()));
    }
}
