using NavySimulator.Engine.Random;

namespace NavySimulator.Engine.Battles.Replay;

/// <summary>
/// How one replayed hour compared: the first roll divergence (null: the engine made exactly the game's rolls), shot
/// counts, shots that match position by position, the first differing shot, end-of-hour state differences, and the HP
/// each side's shots took off in the game and in the engine (by side label).
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
    IReadOnlyDictionary<string, double> EngineHpDamage)
{
    public bool Matches => Divergence is null && FirstShotMismatch is null && StateMismatches.Count == 0;
}
