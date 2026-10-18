namespace NavySimulator.Engine.Random;

public sealed class SeededRandomSource(int seed) : IRandomSource
{
    private readonly System.Random random = new(seed);

    public int Seed { get; } = seed;

    public double NextDouble()
    {
        return random.NextDouble();
    }

    public int Next(int maxExclusive)
    {
        return random.Next(maxExclusive);
    }
}
