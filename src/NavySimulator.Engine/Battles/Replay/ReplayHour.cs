using NavySimulator.Engine.Random;

namespace NavySimulator.Engine.Battles.Replay;

/// <summary>
/// One logged game battle hour (S-REPLAY): battle hour index (the engine's; the game's counter is one more), every
/// ship's state at the start and at the end of the hour by engine ship ID, the game's rolls in call order, and its shots.
/// </summary>
public sealed record ReplayHour(
    int Hour,
    IReadOnlyDictionary<string, ReplayShipState> Start,
    IReadOnlyList<ReplayRoll> Rolls,
    IReadOnlyList<ReplayShot> Shots,
    IReadOnlyDictionary<string, ReplayShipState> End);
