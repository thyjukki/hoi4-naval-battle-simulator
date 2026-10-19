namespace NavySimulator.Engine.Stats;

/// <summary>
/// [RE] hoi4.exe keeps combat values in fixed point with 5 decimals (fx5, raw value × 100000). Rules that must flip at
/// the same boundaries as the game (weighted picks, rounded losses) truncate through these helpers.
/// </summary>
public static class FixedPoint
{
    public const long Scale = 100000;

    /// <summary>The raw fx5 value, truncated toward zero.</summary>
    public static long Raw(double value)
    {
        // The tolerance absorbs binary error in products of fx5 values; their true fractions are multiples of 1e-5 raw.
        return (long)(value >= 0 ? Math.Floor(value * Scale + 1e-7) : Math.Ceiling(value * Scale - 1e-7));
    }

    /// <summary>The value truncated to fx5.</summary>
    public static double Truncate(double value)
    {
        return Raw(value) / (double)Scale;
    }

    /// <summary>
    /// <paramref name="numerator"/> / <paramref name="denominator"/> as the game's fx5 division: both taken to the
    /// nearest fx5 value, the quotient rounded half up. [CAL] The target weight's HP ratio needs the rounding: a truncated
    /// quotient flips one of 120,090 logged target picks.
    /// </summary>
    public static double Divide(double numerator, double denominator)
    {
        var a = (long)Math.Round(numerator * Scale, MidpointRounding.AwayFromZero);
        var b = (long)Math.Round(denominator * Scale, MidpointRounding.AwayFromZero);
        var quotient = (a * Scale + (a >= 0 ? b / 2 : -b / 2)) / b;
        return quotient / (double)Scale;
    }
}
