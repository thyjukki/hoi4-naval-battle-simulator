using NavySimulator.Engine.Random;
using NavySimulator.Engine.Results;

namespace NavySimulator.Engine.Battles;

/// <summary>
/// Runs a battle to its end. The hour loop itself lives in <see cref="BattleSession"/>, which can also be advanced
/// hour by hour.
/// </summary>
public sealed class BattleEngine
{
    public BattleResult Simulate(BattleScenario scenario, int seed)
    {
        return Simulate(scenario, new SeededRandomSource(seed), seed);
    }

    public BattleResult Simulate(BattleScenario scenario, IRandomSource random, int seed = 0)
    {
        var session = new BattleSession(scenario, random, seed);
        session.RunToEnd();
        return session.BuildResult();
    }
}
