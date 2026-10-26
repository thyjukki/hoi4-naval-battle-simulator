namespace NavySimulator.Engine.Random;

/// <summary>
/// Every draw is the same value (S-REPLAY-CONSTANT): a battle fought with one constant roll, as the game's when
/// <c>tools/ingame</c> forces its combat rolls to that value. With no sequence to keep in step, the engine and the game
/// should fight the same battle from the same start.
/// </summary>
public sealed class ConstantRandomSource(double value) : IRandomSource
{
    public double Value { get; } = value;

    public double NextDouble()
    {
        return Value;
    }

    public int Next(int maxExclusive)
    {
        return (int)(Value * maxExclusive);
    }
}
