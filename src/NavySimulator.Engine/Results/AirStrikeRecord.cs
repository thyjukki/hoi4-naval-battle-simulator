namespace NavySimulator.Engine.Results;

/// <summary>One air group's strike in an air hour, with the intermediate values of A-* in docs/mechanics/air.md.</summary>
public sealed record AirStrikeRecord(
    int Hour,
    string Side,
    string Source,
    string TargetId,
    int Flying,
    int Intercepted,
    int ShotDownByAntiAir,
    int Hits,
    double AntiAirReduction,
    bool Critical,
    double HpDamage,
    double OrganizationDamage,
    bool KillingBlow);
