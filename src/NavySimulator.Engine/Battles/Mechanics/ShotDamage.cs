namespace NavySimulator.Engine.Battles.Mechanics;

public readonly record struct ShotDamage(
    double Damage,
    int PiercingTier,
    double PiercingDamageFactor,
    bool IsCritical,
    double CriticalMultiplier,
    bool IsPartHit,
    double RandomFactor);
