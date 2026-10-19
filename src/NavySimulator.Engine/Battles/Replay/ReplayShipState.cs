namespace NavySimulator.Engine.Battles.Replay;

/// <summary>
/// A ship's combat state as the game logged it (S-REPLAY): status, whether it has sunk, HP, organization, manpower,
/// experience, escape progress and the charge of each weapon slot (<see cref="WeaponSlot"/> order).
/// </summary>
public sealed record ReplayShipState(
    ShipStatus Status,
    bool Sunk,
    double Hp,
    double Organization,
    int Manpower,
    double Experience,
    double RetreatProgress,
    double[] Charges);
