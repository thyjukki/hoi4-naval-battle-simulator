namespace NavySimulator.Engine.Stats;

/// <summary>
/// [RE] hoi4.exe's fx5 math library (raw values × 100000, int64, truncating): sine as the 3-term Taylor series on
/// [0, π/2] after range reduction (0x24ef700, 0x24efd30), cosine as sine of x + π/2 (0x24ef840), and the vector
/// normalize with an integer Newton square root (0x24fc2f0). The darkness (S-NIGHT) needs them bit for bit: one map
/// pixel moves a twilight value by 0.03, and doubles miss by up to 0.01.
/// </summary>
public static class GameMath
{
    public const long One = FixedPoint.Scale;

    // [RE] The library's constants: π, π/2, 2π (0x24efd30) and the π used to fold [π/2, π] back (314160).
    public const long Pi = 314159;
    public const long HalfPi = 157080;
    public const long TwoPi = 628319;
    private const long FoldPi = 314160;

    /// <summary>The raw fx5 value of a define: <c>round(float32(value) × 100000)</c>.</summary>
    public static long Define(double value)
    {
        return (long)Math.Round((double)(float)value * One, MidpointRounding.AwayFromZero);
    }

    public static long Multiply(long a, long b)
    {
        return a * b / One;
    }

    public static long Divide(long a, long b)
    {
        return a * One / b;
    }

    public static long Sin(long x)
    {
        var reduced = x - x / TwoPi * TwoPi;

        if (reduced < 0)
        {
            reduced += TwoPi;
        }

        var negative = reduced > Pi;

        if (negative)
        {
            reduced -= Pi;
        }

        var angle = reduced <= HalfPi ? reduced : FoldPi - reduced;
        var sine = Taylor(angle);
        return negative ? -sine : sine;
    }

    public static long Cos(long x)
    {
        return Sin(x + HalfPi);
    }

    /// <summary>The vector divided by its integer length; (−1, −1, −1) for a zero vector, as the game returns.</summary>
    public static (long X, long Y, long Z) Normalize(long x, long y, long z)
    {
        var length = (long)unchecked((int)SquareRoot(x * x + y * y + z * z));

        if (length == 0)
        {
            return (-1, -1, -1);
        }

        return (x * One / length, y * One / length, z * One / length);
    }

    // x - x³/6 + x⁵/120, each term truncated.
    private static long Taylor(long x)
    {
        var sum = 0L;
        var power = x;
        var factorial = 1L;
        var next = 2L;
        var add = true;

        for (var term = 0; term < 3; term++)
        {
            var value = power * One / (factorial * One);
            sum = add ? sum + value : sum - value;
            add = !add;
            power = Multiply(Multiply(power, x), x);
            factorial *= (next + 1) * next;
            next += 2;
        }

        return sum;
    }

    private static long SquareRoot(long n)
    {
        if (n <= 1)
        {
            return n;
        }

        var x = n / 2;

        while (true)
        {
            var previous = x;
            x = (n / x + x) / 2;

            if (x >= previous)
            {
                return previous;
            }
        }
    }
}
