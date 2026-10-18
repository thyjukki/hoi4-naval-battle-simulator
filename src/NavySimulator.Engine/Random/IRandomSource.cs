namespace NavySimulator.Engine.Random;

/// <summary>
/// Every random draw in a battle goes through this interface so a seed fully determines a run,
/// and tests can script exact rolls.
/// </summary>
public interface IRandomSource
{
    /// <summary>Uniform value in [0, 1).</summary>
    double NextDouble();

    /// <summary>Uniform integer in [0, maxExclusive).</summary>
    int Next(int maxExclusive);
}
