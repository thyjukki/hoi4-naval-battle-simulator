namespace NavySimulator.Engine;

/// <summary>
/// A-COVERAGE: one of a side's air bases in the battle: the base, its air base building level (null: crowding not
/// counted), the planes stationed there besides the scenario's land-based wings (other wings, other countries), and its
/// air supply ratio (received / asked, which needs the game's supply network; an input, default 1).
/// </summary>
public sealed record AirBaseSetup(AirBase Base, int? Level = null, int OtherPlanes = 0, double SupplyRatio = 1.0);
