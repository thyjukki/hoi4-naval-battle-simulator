using NavySimulator.Domain.Battles;

namespace NavySimulator.Domain;

public class BattleResult
{
    public string ScenarioID;
    public int HoursElapsed;
    public string Outcome;
    public int AttackerShipsRemaining;
    public int DefenderShipsRemaining;
    public int AttackerShipsRetreated;
    public int DefenderShipsRetreated;
    public double AttackerProductionLost;
    public double DefenderProductionLost;
    public double AttackerToDefenderProductionLossRatio;
    public double DefenderToAttackerProductionLossRatio;
    public PlaneStrength AttackerPlanesAtStart;
    public PlaneStrength DefenderPlanesAtStart;
    public PlaneStrength AttackerPlanesLost;
    public PlaneStrength DefenderPlanesLost;
    public List<string> HourlyLog;
    public List<ShipBattleReport> ShipReports;

    public BattleResult(
        string scenarioId,
        int hoursElapsed,
        string outcome,
        int attackerShipsRemaining,
        int defenderShipsRemaining,
        int attackerShipsRetreated,
        int defenderShipsRetreated,
        double attackerProductionLost,
        double defenderProductionLost,
        double attackerToDefenderProductionLossRatio,
        double defenderToAttackerProductionLossRatio,
        PlaneStrength attackerPlanesAtStart,
        PlaneStrength defenderPlanesAtStart,
        PlaneStrength attackerPlanesLost,
        PlaneStrength defenderPlanesLost,
        List<string> hourlyLog,
        List<ShipBattleReport> shipReports)
    {
        ScenarioID = scenarioId;
        HoursElapsed = hoursElapsed;
        Outcome = outcome;
        AttackerShipsRemaining = attackerShipsRemaining;
        DefenderShipsRemaining = defenderShipsRemaining;
        AttackerShipsRetreated = attackerShipsRetreated;
        DefenderShipsRetreated = defenderShipsRetreated;
        AttackerProductionLost = attackerProductionLost;
        DefenderProductionLost = defenderProductionLost;
        AttackerToDefenderProductionLossRatio = attackerToDefenderProductionLossRatio;
        DefenderToAttackerProductionLossRatio = defenderToAttackerProductionLossRatio;
        AttackerPlanesAtStart = attackerPlanesAtStart;
        DefenderPlanesAtStart = defenderPlanesAtStart;
        AttackerPlanesLost = attackerPlanesLost;
        DefenderPlanesLost = defenderPlanesLost;
        HourlyLog = hourlyLog;
        ShipReports = shipReports;
    }
}

