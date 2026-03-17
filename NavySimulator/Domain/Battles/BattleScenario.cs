namespace NavySimulator.Domain;

public class BattleScenario
{
    public string ID;
    public string Terrain;
    public string Weather;
    public int MaxHours;
    public BattleParticipant Attacker;
    public BattleParticipant Defender;

    public BattleScenario(
        string id,
        string terrain,
        string weather,
        int maxHours,
        BattleParticipant attacker,
        BattleParticipant defender)
    {
        ID = id;
        Terrain = terrain;
        Weather = weather;
        MaxHours = maxHours;
        Attacker = attacker;
        Defender = defender;
    }
}


