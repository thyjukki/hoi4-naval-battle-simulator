namespace NavySimulator.Domain;

public class BattleScenario
{
    public readonly string ID;
    public readonly string Terrain;
    public readonly string Weather;
    public readonly int MaxHours;
    public readonly int Iterations;
    public readonly BattleParticipant Attacker;
    public readonly BattleParticipant Defender;
    public readonly IReadOnlyDictionary<string, PlaneEquipment> PlanesByID;
    public readonly bool ContinueAfterRetreat;
    public readonly bool DontRetreat;

    public BattleScenario(
        string id,
        string terrain,
        string weather,
        int maxHours,
        int iterations,
        BattleParticipant attacker,
        BattleParticipant defender,
        IReadOnlyDictionary<string, PlaneEquipment> planesById,
        bool continueAfterRetreat,
        bool dontRetreat)
    {
        ID = id;
        Terrain = terrain;
        Weather = weather;
        MaxHours = maxHours;
        Iterations = iterations;
        Attacker = attacker;
        Defender = defender;
        PlanesByID = planesById;
        ContinueAfterRetreat = continueAfterRetreat;
        DontRetreat = dontRetreat;
    }
}


