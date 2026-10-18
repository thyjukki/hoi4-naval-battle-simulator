namespace NavySimulator.Engine.Battles.Mechanics;

public readonly record struct HitChanceBreakdown(
    double BaseChance,
    double ShipHitProfile,
    double WeaponHitProfile,
    double ProfileFactor,
    double Modifier,
    double Final);
