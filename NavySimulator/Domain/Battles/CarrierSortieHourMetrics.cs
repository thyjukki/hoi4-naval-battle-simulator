namespace NavySimulator.Domain.Battles;

internal sealed class CarrierSortieHourMetrics(
    int sortiePlanes,
    int planesLost,
    string selectedTargets,
    double targetAntiAirDefense,
    double combinedFleetAaDamageReduction,
    double finalDamageDealt)
{
    public int SortiePlanes = sortiePlanes;
    public int PlanesLost = planesLost;
    public readonly string SelectedTargets = selectedTargets;
    public readonly double TargetAntiAirDefense = targetAntiAirDefense;
    public readonly double CombinedFleetAaDamageReduction = combinedFleetAaDamageReduction;
    public double FinalDamageDealt = finalDamageDealt;
}

