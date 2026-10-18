namespace NavySimulator.Engine.Results;

/// <summary>
/// One side in one battle hour. Line counts, positioning and screening are the start-of-hour values the hour was
/// fought with; ship counts, HP and organization are taken at the end of the hour; fire totals are this hour's.
/// </summary>
public sealed record SideHourState(
    int Screens,
    int Capitals,
    int Carriers,
    int Submarines,
    int Convoys,
    double Positioning,
    double ScreeningEfficiency,
    double CarrierScreeningEfficiency,
    int ShipsInBattle,
    int ShipsSunk,
    int ShipsRetreated,
    double Hp,
    double Organization,
    int ShotsFired,
    int Hits,
    double HpDamageDealt,
    double AirHpDamageDealt);
