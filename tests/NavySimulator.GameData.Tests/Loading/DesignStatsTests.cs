using System.Text.Json.Nodes;
using NavySimulator.Engine.Stats;

namespace NavySimulator.GameData.Tests.Loading;

/// <summary>
/// S-STATS-* regression against values computed with the formulas decoded from hoi4.exe 1.19.3 and the TFB script
/// values (docs/reverse-engineering/findings/S-STATS-design-stats.md, worked examples).
/// </summary>
public sealed class DesignStatsTests : IDisposable
{
    // The battle line tree (TFB) with the traits of the in-game check: armor and the light attack branch.
    private const string Mio = "USA_brooklyn_naval_yard_organization";

    private static readonly string[] MioTraits =
    [
        "generic_mio_trait_internal_armor_belt", "generic_mio_trait_hardened_critical_components",
        "generic_mio_trait_advanced_armor_design", "generic_mio_trait_anti_air_layout",
        "generic_mio_trait_secondary_battery_layout", "generic_mio_trait_gyro_stabilized_mounts",
        "generic_mio_trait_secondary_fire_director_control", "generic_mio_trait_combat_information_center",
        "generic_mio_trait_primary_battery_layout", "generic_mio_trait_semi_armor_piercing_shells"
    ];
    private readonly string dataDirectory;

    public DesignStatsTests()
    {
        dataDirectory = Path.Combine(Path.GetTempPath(), "navysim-stats-" + Guid.NewGuid().ToString("N"));
        DataDirectoryCopy.CopyDirectory(RepositoryPaths.Data, dataDirectory);

        var designs = new JsonArray
        {
            WithMio("battleships.json", "39_battleship_balanced", "test_bb_mio"),
            WithMio("light-cruisers.json", "cl3_armor4_full_la", "test_cl_mio")
        };
        File.WriteAllText(Path.Combine(dataDirectory, "ship-designs", "zz-test.json"), new JsonObject { ["shipDesigns"] = designs }.ToJsonString());

        var fleets = new JsonArray
        {
            Fleet("test_bb", "test_bb_mio"),
            Fleet("test_cl", "test_cl_mio"),
            Fleet("test_cl_mix", "cl3_armor4_mix"),
            Fleet("test_tc", "torpedo_cruiser_torpattack")
        };
        File.WriteAllText(Path.Combine(dataDirectory, "force-compositions", "zz-test.json"), new JsonObject { ["fleets"] = fleets }.ToJsonString());
    }

    public void Dispose()
    {
        Directory.Delete(dataDirectory, true);
    }

    [Fact]
    public void MioJoinsTheModuleMultiplierSum()
    {
        var stats = Load("test_bb", []);

        Assert.Equal(540, stats.Hp, 6);
        Assert.Equal(51.0, stats.Armor, 6);
        Assert.Equal(23.4, stats.AntiAir, 6);
        Assert.Equal(0, stats.TorpedoEnemyCriticalChanceFactor, 6);
        Assert.Equal(14178.0, stats.ProductionCost, 3);
    }

    [Fact]
    public void TechnologiesScaleTheDesignOnce()
    {
        var stats = Load("test_cl", ["light_upgrade_1", "light_upgrade_2", "fire_control_methods_3_cruiser"]);

        Assert.Equal(6.125, stats.AntiAir, 6);
    }

    [Fact]
    public void TechnologyHitChanceFactorsScaleAndAdd()
    {
        var stats = Load("test_cl_mix", ["torpedo_upgrade_1", "torpedo_upgrade_2"]);

        Assert.Equal(0.0815, stats.TorpedoHitChanceFactor, 6);
    }

    [Fact]
    public void NamedEquipmentBonusesAreMultiplicative()
    {
        var stats = Load("test_tc", ["sp_torpedo_cruiser_eq_bonus_torpedo"]);

        Assert.Equal(0.063, stats.TorpedoHitChanceFactor, 6);
    }

    private ShipStats Load(string fleetId, string[] researchIds)
    {
        var scenario = new JsonObject
        {
            ["battleScenario"] = new JsonObject
            {
                ["id"] = "stats_test",
                ["terrain"] = "ocean",
                ["weather"] = "clear",
                ["maxHours"] = 1,
                ["iterations"] = 1,
                ["attacker"] = Participant(fleetId, researchIds),
                ["defender"] = Participant(fleetId, [])
            }
        };
        var path = Path.Combine(dataDirectory, "zz-stats-scenario.json");
        File.WriteAllText(path, scenario.ToJsonString());

        var loaded = new SetupLoader().LoadScenarioFromDirectory(dataDirectory, "zz-stats-scenario.json");
        return loaded.Attacker.Fleet.Ships.Single().GetFinalStats();
    }

    private static JsonObject Participant(string fleetId, string[] researchIds)
    {
        return new JsonObject
        {
            ["fleetID"] = fleetId,
            ["commander"] = "",
            ["researchIDs"] = new JsonArray(researchIds.Select(id => (JsonNode)JsonValue.Create(id)!).ToArray()),
            ["spiritIDs"] = new JsonArray(),
            ["shipExperienceLevel"] = 0,
            ["mios"] = new JsonObject
            {
                [Mio] = new JsonObject { ["traits"] = new JsonArray(MioTraits.Select(id => (JsonNode)JsonValue.Create(id)!).ToArray()) }
            }
        };
    }

    private static JsonObject Fleet(string id, string designId)
    {
        return new JsonObject { ["id"] = id, ["shipDesigns"] = new JsonObject { [designId] = 1 } };
    }

    private JsonNode WithMio(string file, string designId, string newId)
    {
        var root = JsonNode.Parse(File.ReadAllText(Path.Combine(dataDirectory, "ship-designs", file)))!;
        var design = root["shipDesigns"]!.AsArray().Single(node => (string?)node!["id"] == designId)!.DeepClone();
        design["id"] = newId;
        design["mioID"] = Mio;
        return design;
    }
}
