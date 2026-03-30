namespace NavySimulator.Domain.Battles;

public sealed record ShipBattleReport(
    string ShipID,
    string Side,
    bool IsSunk,
    bool DidRetreat,
    bool AttemptedRetreat,
    bool AttemptedRetreatButSunk,
    double CurrentHp,
    double MaxHp,
    double HpPercentage,
    double ProductionCost,
    double TotalDamageDone,
    int CarrierPlaneSorties,
    List<CarrierSortieReportEntry> CarrierSortiesByHour,
    List<ShipDamageReportEntry> DamagedShips);

