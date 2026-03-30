using NavySimulator.Domain.Battles;

namespace NavySimulator.Domain;

public class BattleResult(
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
    double attackerCarrierPlaneDamage,
    double defenderCarrierPlaneDamage,
    Dictionary<string, double> attackerPlaneDamageByType,
    Dictionary<string, double> defenderPlaneDamageByType,
    List<string> hourlyLog,
    List<ShipBattleReport> shipReports)
{
    public readonly string ScenarioId = scenarioId;
    public readonly int HoursElapsed = hoursElapsed;
    public readonly string Outcome = outcome;
    public readonly int AttackerShipsRemaining = attackerShipsRemaining;
    public readonly int DefenderShipsRemaining = defenderShipsRemaining;
    public readonly int AttackerShipsRetreated = attackerShipsRetreated;
    public readonly int DefenderShipsRetreated = defenderShipsRetreated;
    public readonly double AttackerProductionLost = attackerProductionLost;
    public readonly double DefenderProductionLost = defenderProductionLost;
    public readonly double AttackerToDefenderProductionLossRatio = attackerToDefenderProductionLossRatio;
    public readonly double DefenderToAttackerProductionLossRatio = defenderToAttackerProductionLossRatio;
    public readonly PlaneStrength AttackerPlanesAtStart = attackerPlanesAtStart;
    public readonly PlaneStrength DefenderPlanesAtStart = defenderPlanesAtStart;
    public readonly PlaneStrength AttackerPlanesLost = attackerPlanesLost;
    public readonly PlaneStrength DefenderPlanesLost = defenderPlanesLost;
    public readonly double AttackerCarrierPlaneDamage = attackerCarrierPlaneDamage;
    public readonly double DefenderCarrierPlaneDamage = defenderCarrierPlaneDamage;
    public readonly Dictionary<string, double> AttackerPlaneDamageByType = attackerPlaneDamageByType;
    public readonly Dictionary<string, double> DefenderPlaneDamageByType = defenderPlaneDamageByType;
    public readonly List<string> HourlyLog = hourlyLog;
    public readonly List<ShipBattleReport> ShipReports = shipReports;
}

