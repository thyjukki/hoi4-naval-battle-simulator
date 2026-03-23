namespace NavySimulator.Domain.Battles;

public class CarrierSortieReportEntry
{
    public int HourTick;
    public int SortiePlanes;
    public int PlanesLost;
    public string SelectedTargets;
    public double TargetAntiAirDefense;
    public double CombinedFleetAaDamageReduction;
    public double FinalDamageDealt;

    public CarrierSortieReportEntry(
        int hourTick,
        int sortiePlanes,
        int planesLost,
        string selectedTargets,
        double targetAntiAirDefense,
        double combinedFleetAaDamageReduction,
        double finalDamageDealt)
    {
        HourTick = hourTick;
        SortiePlanes = sortiePlanes;
        PlanesLost = planesLost;
        SelectedTargets = selectedTargets;
        TargetAntiAirDefense = targetAntiAirDefense;
        CombinedFleetAaDamageReduction = combinedFleetAaDamageReduction;
        FinalDamageDealt = finalDamageDealt;
    }
}



