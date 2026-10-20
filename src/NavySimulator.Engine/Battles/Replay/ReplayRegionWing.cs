namespace NavySimulator.Engine.Battles.Replay;

/// <summary>
/// A-REGION check of one wing in one air hour of a replay: the engine's region air war run from the game's wings at the
/// start of the hour, against what the game's wing had at its strike: disruption (after escorts) and planes lost.
/// </summary>
public sealed record ReplayRegionWing(int Hour, string Side, string Source, string Plane, uint WingId, int StartPlanes,
    double EngineDisruption, double GameDisruption, int EngineLosses, int GameLosses);
