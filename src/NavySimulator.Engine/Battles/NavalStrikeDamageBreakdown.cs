namespace NavySimulator.Engine.Battles;

internal sealed record NavalStrikeDamageBreakdown(
    double FinalDamageBeforeHpClamp,
    double TargetedAaDefense,
    double CombinedFleetAaDamageReduction);

