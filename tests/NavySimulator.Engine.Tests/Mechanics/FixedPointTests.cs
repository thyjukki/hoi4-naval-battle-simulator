using NavySimulator.Engine.Stats;

namespace NavySimulator.Engine.Tests.Mechanics;

public class FixedPointTests
{
    [Theory]
    [InlineData(1.234569, 1.23456)]
    [InlineData(-1.234569, -1.23456)]
    [InlineData(6 * 1.66667, 10.00002)]
    public void Truncate_DropsDigitsBeyondTheFifthDecimal(double value, double expected)
    {
        Assert.Equal(expected, FixedPoint.Truncate(value), 9);
    }

    [Theory]
    [InlineData(1, 3, 0.33333)]
    [InlineData(2, 3, 0.66667)]
    [InlineData(37.2205, 770, 0.04834)]
    public void Divide_RoundsTheQuotientHalfUp(double numerator, double denominator, double expected)
    {
        Assert.Equal(expected, FixedPoint.Divide(numerator, denominator), 9);
    }
}
