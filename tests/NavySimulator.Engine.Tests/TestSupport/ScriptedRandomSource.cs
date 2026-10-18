using NavySimulator.Engine.Random;

namespace NavySimulator.Engine.Tests.TestSupport;

/// <summary>Returns the given values in order, then repeats the last one.</summary>
internal sealed class ScriptedRandomSource(params double[] values) : IRandomSource
{
    private int index;

    public double NextDouble()
    {
        var value = values[Math.Min(index, values.Length - 1)];
        index++;
        return value;
    }

    public int Next(int maxExclusive)
    {
        return Math.Min(maxExclusive - 1, (int)(NextDouble() * maxExclusive));
    }
}
