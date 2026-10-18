using NavySimulator.Engine.Battles;

namespace NavySimulator.Engine.Results;

/// <summary>
/// One weapon firing at a target, with every intermediate value needed to audit it against the spec.
/// </summary>
public sealed record ShotRecord(
    int Hour,
    string ShooterSide,
    string ShooterId,
    WeaponType Weapon,
    string TargetId,
    GroupType TargetGroup,
    double HitChance,
    double ProfileFactor,
    double HitModifier,
    bool Hit,
    double Damage,
    int PiercingTier,
    bool Critical,
    bool PartHit,
    double HpDamage,
    double OrganizationDamage,
    bool KillingBlow);
