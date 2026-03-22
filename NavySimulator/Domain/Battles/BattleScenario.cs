namespace NavySimulator.Domain;

public class BattleScenario
{
    public string ID;
    public string Terrain;
    public string Weather;
    public int MaxHours;
    public int Iterations;
    public BattleParticipant Attacker;
    public BattleParticipant Defender;
    public IReadOnlyDictionary<string, PlaneEquipment> PlanesByID;

    public BattleScenario(
        string id,
        string terrain,
        string weather,
        int maxHours,
        int iterations,
        BattleParticipant attacker,
        BattleParticipant defender,
        IReadOnlyDictionary<string, PlaneEquipment> planesById)
    {
        ID = id;
        Terrain = terrain;
        Weather = weather;
        MaxHours = maxHours;
        Iterations = iterations;
        Attacker = attacker;
        Defender = defender;
        PlanesByID = planesById;
    }
}


