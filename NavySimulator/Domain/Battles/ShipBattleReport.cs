namespace NavySimulator.Domain.Battles;

public class ShipBattleReport
{
    public string ShipID;
    public string Side;
    public bool IsSunk;
    public bool DidRetreat;
    public bool AttemptedRetreat;
    public bool AttemptedRetreatButSunk;
    public double CurrentHp;
    public double MaxHp;
    public double HpPercentage;
    public double ProductionCost;
    public double TotalDamageDone;
    public List<ShipDamageReportEntry> DamagedShips;

    public ShipBattleReport(
        string shipId,
        string side,
        bool isSunk,
        bool didRetreat,
        bool attemptedRetreat,
        bool attemptedRetreatButSunk,
        double currentHp,
        double maxHp,
        double hpPercentage,
        double productionCost,
        double totalDamageDone,
        List<ShipDamageReportEntry> damagedShips)
    {
        ShipID = shipId;
        Side = side;
        IsSunk = isSunk;
        DidRetreat = didRetreat;
        AttemptedRetreat = attemptedRetreat;
        AttemptedRetreatButSunk = attemptedRetreatButSunk;
        CurrentHp = currentHp;
        MaxHp = maxHp;
        HpPercentage = hpPercentage;
        ProductionCost = productionCost;
        TotalDamageDone = totalDamageDone;
        DamagedShips = damagedShips;
    }
}

