using NavySimulator.Engine.Results;

namespace NavySimulator.Engine.Battles.Replay;

/// <summary>The first shot of an hour where engine and game differ: its index in the hour, both shots (null if one side fired fewer) and what differs.</summary>
public sealed record ReplayShotMismatch(int Index, ReplayShot? Game, ShotRecord? Engine, string Difference);
