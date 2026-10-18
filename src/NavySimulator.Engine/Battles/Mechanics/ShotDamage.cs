namespace NavySimulator.Engine.Battles.Mechanics;

public readonly record struct ShotDamage(
    double Damage,
    double StrDamage,
    double OrgDamage,
    int PiercingTier,
    double PiercingDamageFactor,
    bool IsCritical,
    double CriticalMultiplier,
    bool IsPartHit,
    double RandomFactor);
