using NavySimulator.Engine.Defines;
using NavySimulator.Engine.Environment;

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

    /// <summary>A-ACE: the air ace templates (<c>common/aces</c>) aces are drawn from; empty: no aces.</summary>
    public readonly IReadOnlyList<AceTemplate> AceTemplates;

    /// <summary>S-REGION: the battle's region, province, naval terrain and start date; unknown parts are missing.</summary>
    public readonly BattleLocation Location;

    /// <summary>S-WEATHER: fixed weather or a pattern simulated from the region's weather periods.</summary>
    public readonly WeatherSetup WeatherSetup;

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
        BattleRules rules,
        IReadOnlyList<AceTemplate>? aceTemplates = null,
        BattleLocation? location = null,
        WeatherSetup? weatherSetup = null)
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
        AceTemplates = aceTemplates ?? [];
        Location = location ?? BattleLocation.Unknown;
        WeatherSetup = weatherSetup ?? WeatherSetup.Clear;
    }

    /// <summary>The same scenario with other rules (e.g. a replay of a battle logged in an older game version).</summary>
    public BattleScenario WithRules(BattleRules rules)
    {
        return new BattleScenario(ID, Terrain, Weather, Iterations, Seed, Attacker, Defender, PlanesByID, Defines, rules,
            AceTemplates, Location, WeatherSetup);
    }
}
