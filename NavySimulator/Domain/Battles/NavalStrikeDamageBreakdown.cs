namespace NavySimulator.Domain.Battles;

internal sealed class NavalStrikeDamageBreakdown
{
    public double FinalDamageBeforeHpClamp;
    public double TargetedAaDefense;
    public double CombinedFleetAaDamageReduction;

    public NavalStrikeDamageBreakdown(
        double finalDamageBeforeHpClamp,
        double targetedAaDefense,
        double combinedFleetAaDamageReduction)
    {
        FinalDamageBeforeHpClamp = finalDamageBeforeHpClamp;
        TargetedAaDefense = targetedAaDefense;
        CombinedFleetAaDamageReduction = combinedFleetAaDamageReduction;
    }
}

