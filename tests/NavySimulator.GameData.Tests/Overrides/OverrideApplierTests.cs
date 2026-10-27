using System.Text.Json.Nodes;
using NavySimulator.Engine.Defines;
using NavySimulator.GameData.Contracts;
using NavySimulator.GameData.Defines;
using NavySimulator.GameData.Overrides;

namespace NavySimulator.GameData.Tests.Overrides;

public class OverrideApplierTests
{
    private static readonly ContentCatalog Catalog = new SetupLoader().ReadCatalog(RepositoryPaths.Data);

    [Fact]
    public void ContentChange_ReplacesTheItemInACopy_AndLeavesTheCatalogAsRead()
    {
        var original = Catalog.Modules.Single(module => module.ID == "ship_light_battery_3");
        var before = original.StatModifiers.LightAttack;
        var set = Set(new ContentOverrideDto { Collection = "modules", ID = original.ID, Path = "statModifiers.lightAttack", Value = before + 10 });
        var warnings = new List<string>();

        var patched = OverrideApplier.ApplyContent(Catalog, set, warnings);

        Assert.Empty(warnings);
        Assert.NotSame(Catalog, patched);
        Assert.Equal(before + 10, patched.Modules.Single(module => module.ID == original.ID).StatModifiers.LightAttack);
        Assert.Equal(before, original.StatModifiers.LightAttack);
        Assert.Same(original, Catalog.Modules.Single(module => module.ID == original.ID));
        Assert.Equal(Catalog.SourceFileOf(original), patched.SourceFileOf(patched.Modules.Single(module => module.ID == original.ID)));
        Assert.Same(Catalog.ShipDesigns[0], patched.ShipDesigns[0]);
    }

    [Fact]
    public void ContentChange_ReachesListEntriesByIdAndAddsDictionaryKeys()
    {
        var set = Set(
            new ContentOverrideDto { Collection = "doctrines", ID = "naval", Path = "subdoctrines[line_of_battle].rewards[flagships].mastery", Value = 42 },
            new ContentOverrideDto { Collection = "doctrines", ID = "naval", Path = "grandDoctrines[new_fleet_in_being].effects.countryModifiers.naval_hit_chance", Value = 0.5 });
        var warnings = new List<string>();

        var patched = OverrideApplier.ApplyContent(Catalog, set, warnings);

        Assert.Empty(warnings);
        var folder = patched.DoctrineFolders.Single(candidate => candidate.ID == "naval");
        Assert.Equal(42, folder.Subdoctrines.Single(sub => sub.ID == "line_of_battle").Rewards.Single(reward => reward.ID == "flagships").Mastery);
        Assert.Equal(0.5, folder.GrandDoctrines.Single(grand => grand.ID == "new_fleet_in_being").Effects.CountryModifiers["naval_hit_chance"]);
        Assert.Null(Catalog.DoctrineFolders.Single(candidate => candidate.ID == "naval")
            .Subdoctrines.Single(sub => sub.ID == "line_of_battle").Rewards.Single(reward => reward.ID == "flagships").Mastery);
    }

    [Fact]
    public void NullRemovesADictionaryKey()
    {
        var spirit = Catalog.Spirits.First(candidate => candidate.Effects.CountryModifiers.Count > 0);
        var key = spirit.Effects.CountryModifiers.Keys.First();
        var set = Set(new ContentOverrideDto { Collection = "spirits", ID = spirit.ID, Path = $"effects.countryModifiers.{key}", Value = null });

        var patched = OverrideApplier.ApplyContent(Catalog, set, []);

        Assert.DoesNotContain(key, patched.Spirits.Single(candidate => candidate.ID == spirit.ID).Effects.CountryModifiers.Keys);
        Assert.Contains(key, spirit.Effects.CountryModifiers.Keys);
    }

    [Theory]
    [InlineData("nonsense", "ship_light_battery_3", "statModifiers.lightAttack", "unknown collection")]
    [InlineData("modules", "no_such_module", "statModifiers.lightAttack", "has no 'no_such_module'")]
    [InlineData("modules", "ship_light_battery_3", "statModifiers.notAStat", "not a field")]
    [InlineData("modules", "ship_light_battery_3", "id", "cannot be changed")]
    [InlineData("doctrines", "naval", "subdoctrines[no_such].mastery", "no entry 'no_such'")]
    public void ChangesThatCannotApply_AreWarnedAndSkipped(string collection, string id, string path, string expected)
    {
        var set = Set(new ContentOverrideDto { Collection = collection, ID = id, Path = path, Value = 1 });
        var warnings = new List<string>();

        var patched = OverrideApplier.ApplyContent(Catalog, set, warnings);

        Assert.Contains(warnings, warning => warning.Contains(expected, StringComparison.Ordinal));
        Assert.Equal(Catalog.Modules, patched.Modules);
    }

    [Fact]
    public void AValueOfTheWrongType_IsWarnedAndTheOtherChangesStillApply()
    {
        var set = Set(
            new ContentOverrideDto { Collection = "modules", ID = "ship_light_battery_3", Path = "statModifiers.lightAttack", Value = "a lot" },
            new ContentOverrideDto { Collection = "modules", ID = "ship_light_battery_3", Path = "statModifiers.lightPiercing", Value = 99 });
        var warnings = new List<string>();

        var patched = OverrideApplier.ApplyContent(Catalog, set, warnings);

        Assert.Single(warnings);
        Assert.Equal(99, patched.Modules.Single(module => module.ID == "ship_light_battery_3").StatModifiers.LightPiercing);
    }

    [Fact]
    public void Defines_SetNumbersAndArrays_AndStaticModifiersSetOrRemoveKeys()
    {
        var defines = GameDefines.Vanilla();
        var report = new DefinesBindingReport();
        var set = Set();
        set.Defines["NNavy.COMBAT_BASE_HIT_CHANCE"] = 0.2;
        set.Defines["NNavy.BASE_GUN_COOLDOWNS"] = new JsonArray(4, 7, 3);
        set.Defines["NNavy.NOT_A_DEFINE"] = 1;
        set.StaticModifiers[StaticModifiers.ScreeningBonus] = new Dictionary<string, double?> { ["naval_hit_chance"] = 0.1, ["convoy_retreat_speed"] = null };
        var warnings = new List<string>();

        OverrideApplier.ApplyDefines(defines, set, report, warnings);

        Assert.Equal(0.2, defines.NNavy.COMBAT_BASE_HIT_CHANCE);
        Assert.Equal([4.0, 7.0, 3.0], defines.NNavy.BASE_GUN_COOLDOWNS);
        Assert.Equal(0.1, defines.StaticModifiers.Get(StaticModifiers.ScreeningBonus, "naval_hit_chance"));
        Assert.DoesNotContain("convoy_retreat_speed", defines.StaticModifiers.Blocks[StaticModifiers.ScreeningBonus].Keys);
        Assert.Single(warnings, warning => warning.Contains("NNavy.NOT_A_DEFINE", StringComparison.Ordinal));
        Assert.Contains("override set 'test'", defines.Sources);
        Assert.Contains(report.Applied, line => line.StartsWith("NNavy.COMBAT_BASE_HIT_CHANCE", StringComparison.Ordinal));
    }

    [Fact]
    public void BuildScenario_UsesTheSetForThisBuildOnly()
    {
        var scenarioDto = SetupLoader.ReadScenario(RepositoryPaths.Data, "battle-scenario.json");
        var lightAttack = Catalog.Modules.Single(module => module.ID == "ship_light_battery_3").StatModifiers.LightAttack;
        var set = Set(new ContentOverrideDto { Collection = "modules", ID = "ship_light_battery_3", Path = "statModifiers.lightAttack", Value = lightAttack + 10 });
        set.Defines["NNavy.COMBAT_BASE_HIT_CHANCE"] = 0.2;

        var plain = new SetupLoader().BuildScenario(Catalog, scenarioDto);
        var changed = new SetupLoader().BuildScenario(Catalog, scenarioDto, overrides: set);
        var again = new SetupLoader().BuildScenario(Catalog, scenarioDto);

        Assert.Equal(0.2, changed.Defines.NNavy.COMBAT_BASE_HIT_CHANCE);
        Assert.NotEqual(0.2, plain.Defines.NNavy.COMBAT_BASE_HIT_CHANCE);
        Assert.True(changed.Attacker.Fleet.Ships[0].GetFinalStats().LightAttack > plain.Attacker.Fleet.Ships[0].GetFinalStats().LightAttack);
        Assert.Equal(plain.Attacker.Fleet.Ships[0].GetFinalStats(), again.Attacker.Fleet.Ships[0].GetFinalStats());
    }

    [Fact]
    public void BuildScenario_ReportsChangesThatCannotApplyAsWarnings()
    {
        var scenarioDto = SetupLoader.ReadScenario(RepositoryPaths.Data, "battle-scenario.json");
        var set = Set(new ContentOverrideDto { Collection = "modules", ID = "no_such_module", Path = "statModifiers.lightAttack", Value = 1 });
        var loader = new SetupLoader();

        loader.BuildScenario(Catalog, scenarioDto, overrides: set);

        Assert.Contains(loader.Warnings, warning => warning.StartsWith("override set 'test'", StringComparison.Ordinal));
    }

    private static OverrideSetDto Set(params ContentOverrideDto[] content)
    {
        return new OverrideSetDto { ID = "test", Content = [.. content] };
    }
}
