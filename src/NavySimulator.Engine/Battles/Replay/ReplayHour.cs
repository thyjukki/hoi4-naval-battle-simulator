using NavySimulator.Engine.Random;

namespace NavySimulator.Engine.Battles.Replay;

/// <summary>
/// One logged game battle hour (S-REPLAY): battle hour index (the engine's; the game's counter is one more), every
/// ship's state at the start and at the end of the hour by engine ship ID, the game's rolls in call order, its shots, and
/// its air objects' strike attempts in the game's order, and the attacker's and defender's side values at the end and
/// the start of the hour, and each air object's counted planes at the start (by the game's air object pointer).
/// </summary>
public sealed record ReplayHour(
    int Hour,
    IReadOnlyDictionary<string, ReplayShipState> Start,
    IReadOnlyList<ReplayRoll> Rolls,
    IReadOnlyList<ReplayShot> Shots,
    IReadOnlyDictionary<string, ReplayShipState> End,
    IReadOnlyList<ReplayAirStrike>? Air = null,
    IReadOnlyList<ReplaySideState>? Sides = null,
    IReadOnlyList<ReplaySideState>? StartSides = null,
    IReadOnlyDictionary<string, int>? StartAirObjectPlanes = null);
