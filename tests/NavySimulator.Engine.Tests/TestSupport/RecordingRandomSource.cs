using NavySimulator.Engine.Random;

namespace NavySimulator.Engine.Tests.TestSupport;

/// <summary>Draws from a seeded source and records every tagged roll, the way the in-game logger records the game's.</summary>
internal sealed class RecordingRandomSource(int seed) : IRandomSource
{
    private readonly SeededRandomSource inner = new(seed);

    public List<ReplayRoll> Rolls { get; } = [];

    public double NextDouble()
    {
        return NextDouble(RollKind.Unspecified);
    }

    public double NextDouble(RollKind kind)
    {
        var value = inner.NextDouble();
        Rolls.Add(new ReplayRoll(kind, value, 0));
        return value;
    }

    public int Next(int maxExclusive)
    {
        return inner.Next(maxExclusive);
    }
}
