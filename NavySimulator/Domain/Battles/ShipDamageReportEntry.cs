namespace NavySimulator.Domain.Battles;

public sealed record ShipDamageReportEntry(
    int HourTick,
    string TargetShipID,
    WeaponType Weapon,
    bool DidHit,
    double Damage,
    double AppliedHpDamage,
    double AppliedOrganizationDamage,
    bool DidKillingBlow,
    double AttackerPiercing,
    double AttackerFinalHitChance,
    double DefenderArmor,
    double DefenderSpeed,
    double DefenderVisibility);

