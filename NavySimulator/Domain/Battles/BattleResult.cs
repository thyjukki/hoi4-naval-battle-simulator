namespace NavySimulator.Domain;

public class BattleResult
{
    public string ScenarioID;
    public int HoursElapsed;
    public string Outcome;
    public int AttackerShipsRemaining;
    public int DefenderShipsRemaining;
    public List<string> HourlyLog;

    public BattleResult(
        string scenarioId,
        int hoursElapsed,
        string outcome,
        int attackerShipsRemaining,
        int defenderShipsRemaining,
        List<string> hourlyLog)
    {
        ScenarioID = scenarioId;
        HoursElapsed = hoursElapsed;
        Outcome = outcome;
        AttackerShipsRemaining = attackerShipsRemaining;
        DefenderShipsRemaining = defenderShipsRemaining;
        HourlyLog = hourlyLog;
    }
}

