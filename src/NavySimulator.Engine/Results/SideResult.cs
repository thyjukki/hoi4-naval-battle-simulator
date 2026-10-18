namespace NavySimulator.Engine.Results;

public sealed record SideResult(
    string Label,
    string FleetId,
    int ShipsAtStart,
    int ShipsSunk,
    int ShipsRetreated,
    int ShipsRemaining,
    double HpAtStart,
    double HpLost,
    double HpDamageDealt);
