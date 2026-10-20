namespace NavySimulator.Engine.Battles.Replay;

/// <summary>
/// A side's values the game sets in its side update each hour (S-REPLAY): positioning, screening and carrier screening
/// efficiency (S-POS, S-SCREEN) and the fleet AA term (A-AA-REDUCE), as logged at the end of the hour.
/// </summary>
public sealed record ReplaySideState(double Positioning, double Screening, double CarrierScreening, double FleetAntiAir);
