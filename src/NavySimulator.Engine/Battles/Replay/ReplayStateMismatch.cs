namespace NavySimulator.Engine.Battles.Replay;

/// <summary>A ship value that differs at the end of an hour between game and engine.</summary>
public sealed record ReplayStateMismatch(string ShipId, string Field, double Game, double Engine);
