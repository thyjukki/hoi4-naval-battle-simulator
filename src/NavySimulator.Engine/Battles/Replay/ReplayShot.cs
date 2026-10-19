namespace NavySimulator.Engine.Battles.Replay;

/// <summary>
/// One logged game shot: shooter and target (engine ship IDs), weapon, hit, whether it was a critical hit, whether the
/// game rolled for and damaged a part, and the HP and organization the target lost.
/// </summary>
public sealed record ReplayShot(
    string ShooterId,
    string TargetId,
    WeaponType Weapon,
    bool Hit,
    double HpDamage,
    double OrganizationDamage,
    bool Critical = false,
    bool PartRolled = false,
    bool PartDamaged = false);
