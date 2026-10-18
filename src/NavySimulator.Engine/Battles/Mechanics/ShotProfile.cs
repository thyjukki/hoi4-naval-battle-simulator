namespace NavySimulator.Engine.Battles.Mechanics;

/// <summary>
/// [RE] Per (shooter, target, slot) values the game computes once before the shots of a slot (0x1970a90): damage before
/// randomness, crit chance and multiplier, and the torpedo damage reduction factor (null when not a torpedo).
/// </summary>
public readonly record struct ShotProfile(
    double BaseDamage,
    int PiercingTier,
    double PiercingDamageFactor,
    double CriticalChance,
    double CriticalMultiplier,
    double? TorpedoFactor,
    bool IsDepthCharge);
