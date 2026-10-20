using NavySimulator.Engine.Random;

namespace NavySimulator.Engine.Battles.Replay;

/// <summary>
/// How one replayed hour compared: the first roll divergence (null: the engine made exactly the game's rolls), shot
/// counts, shots that match position by position, the first differing shot, end-of-hour state differences, and the HP
/// each side's shots took off in the game and in the engine (by side label), and the air strikes: how many each made,
/// how many match (target, planes shot down, hits, HP and organization damage) and the first difference.
/// </summary>
public sealed record ReplayHourReport(
    int Hour,
    ReplayDivergence? Divergence,
    int SkippedRolls,
    int GameShots,
    int EngineShots,
    int MatchingShots,
    ReplayShotMismatch? FirstShotMismatch,
    IReadOnlyList<ReplayStateMismatch> StateMismatches,
    IReadOnlyDictionary<string, double> GameHpDamage,
    IReadOnlyDictionary<string, double> EngineHpDamage,
    int GameAirStrikes = 0,
    int EngineAirStrikes = 0,
    int MatchingAirStrikes = 0,
    string? FirstAirMismatch = null,
    double GameAirHpDamage = 0,
    double EngineAirHpDamage = 0)
{
    public bool Matches => Divergence is null && FirstShotMismatch is null && StateMismatches.Count == 0 && FirstAirMismatch is null;
}
