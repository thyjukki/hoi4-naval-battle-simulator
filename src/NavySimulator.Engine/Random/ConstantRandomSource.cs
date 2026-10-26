namespace NavySimulator.Engine.Random;

/// <summary>
/// Every draw is the same value (S-REPLAY-CONSTANT): a battle fought with one constant roll, as the game's when
/// <c>tools/ingame</c> forces its rolls to that value. With no sequence to keep in step, the engine and the game should
/// fight the same battle from the same start. The game's integer draws (the air target pick) are forced to
/// <c>floor(U × 2^31)</c> and read as that over 2^31, so those kinds return it.
/// </summary>
public sealed class ConstantRandomSource(double value) : IRandomSource
{
    private const double IntegerScale = 2147483648.0;

    public double Value { get; } = value;

    /// <summary>The value an integer draw of the game gives: floor(raw / 100000 × 2^31) / 2^31, raw the fx5 value of U.</summary>
    public double IntegerValue { get; } = Math.Floor(Math.Round(value * 100000) * IntegerScale / 100000) / IntegerScale;

    public double NextDouble()
    {
        return Value;
    }

    public double NextDouble(RollKind kind)
    {
        return kind == RollKind.AirTargetPick ? IntegerValue : Value;
    }

    public int Next(int maxExclusive)
    {
        return (int)(Value * maxExclusive);
    }
}
