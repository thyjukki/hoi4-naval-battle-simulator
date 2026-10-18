using NavySimulator.Engine.Defines;

namespace NavySimulator.Engine.Battles;

public class BattleScenario
{
    public readonly string ID;
    public readonly string Terrain;
    public readonly string Weather;
    public readonly int Iterations;
    public readonly int? Seed;
    public readonly BattleParticipant Attacker;
    public readonly BattleParticipant Defender;
    public readonly IReadOnlyDictionary<string, PlaneEquipment> PlanesByID;
    public readonly GameDefines Defines;
    public readonly BattleRules Rules;

    public BattleScenario(
        string id,
        string terrain,
        string weather,
        int iterations,
        int? seed,
        BattleParticipant attacker,
        BattleParticipant defender,
        IReadOnlyDictionary<string, PlaneEquipment> planesById,
        GameDefines defines,
        BattleRules rules)
    {
        ID = id;
        Terrain = terrain;
        Weather = weather;
        Iterations = iterations;
        Seed = seed;
        Attacker = attacker;
        Defender = defender;
        PlanesByID = planesById;
        Defines = defines;
        Rules = rules;
    }
}
