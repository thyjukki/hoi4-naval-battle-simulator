namespace NavySimulator.Domain.Battles;

public sealed record CarrierSortieReportEntry(
    int HourTick,
    int SortiePlanes,
    int PlanesLost,
    string SelectedTargets,
    double TargetAntiAirDefense,
    double CombinedFleetAaDamageReduction,
    double FinalDamageDealt);



