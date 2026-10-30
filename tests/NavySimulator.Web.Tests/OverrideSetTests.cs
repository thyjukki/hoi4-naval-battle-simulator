using System.Text.Json.Nodes;
using NavySimulator.GameData.Overrides;
using NavySimulator.Web.Services;

namespace NavySimulator.Web.Tests;

public sealed class OverrideSetTests : IDisposable
{
    private const string Module = "ship_light_battery_3";
    private const string LightAttack = "statModifiers.lightAttack";

    private readonly TestWorkspace test = new();

    private Workspace Workspace => test.Workspace;

    public OverrideSetTests()
    {
        // These tests count the sets, so they start without the ones in the repository's data.
        foreach (var file in OverrideSetStore.ListFiles(test.DataDirectory))
        {
            File.Delete(Path.Combine(test.DataDirectory, file));
        }

        Workspace.Overrides.Reload();
    }

    public void Dispose()
    {
        test.Dispose();
    }

    [Fact]
    public void Save_WritesTheSet_RenamingMovesTheFile_DeleteRemovesIt()
    {
        var entry = Workspace.Overrides.Create("cl-buff");
        Workspace.Overrides.SetDefine(entry.Set, "NNavy.COMBAT_BASE_HIT_CHANCE", 0.2);

        Assert.True(entry.IsDirty);
        Workspace.Overrides.Save(entry);

        Assert.False(entry.IsDirty);
        Assert.True(File.Exists(Path.Combine(test.DataDirectory, "overrides", "cl-buff.json")));

        entry.Set.ID = "cl-buff-2";
        Workspace.Overrides.Save(entry);

        Assert.Equal("overrides/cl-buff-2.json", entry.File);
        Assert.False(File.Exists(Path.Combine(test.DataDirectory, "overrides", "cl-buff.json")));

        Workspace.Overrides.Reload();
        var reread = Assert.Single(Workspace.Overrides.Entries);
        Assert.Equal(0.2, OverrideValues.Number(reread.Set.Defines["NNavy.COMBAT_BASE_HIT_CHANCE"]!));

        Workspace.Overrides.Delete(reread);
        Assert.Empty(Workspace.Overrides.Entries);
        Assert.Empty(OverrideSetStore.ListFiles(test.DataDirectory));
    }

    [Fact]
    public void Create_GivesAUniqueId_AndRevertDropsAnUnsavedSet()
    {
        var first = Workspace.Overrides.Create("same");
        var second = Workspace.Overrides.Create("same");

        Assert.NotEqual(first.Set.ID, second.Set.ID);

        Workspace.Overrides.Revert(second);
        Assert.DoesNotContain(second, Workspace.Overrides.Entries);
    }

    [Fact]
    public void TheActiveSet_ChangesContentAndBattles_UntilItIsSwitchedOff()
    {
        var before = Workspace.Catalog.Modules.Single(module => module.ID == Module).StatModifiers.LightAttack;
        var entry = Workspace.Overrides.Create("active");
        Workspace.Overrides.SetContent(entry.Set, "modules", Module, LightAttack, before + 10);
        Workspace.Overrides.SetDefine(entry.Set, "NNavy.COMBAT_BASE_HIT_CHANCE", 0.2);

        Workspace.UseOverrideSet(entry.File);

        Assert.Same(entry, Workspace.ActiveOverrides);
        Assert.Equal(before + 10, Workspace.Content.Modules.Single(module => module.ID == Module).StatModifiers.LightAttack);
        Assert.Equal(before, Workspace.Catalog.Modules.Single(module => module.ID == Module).StatModifiers.LightAttack);
        Assert.Equal(before + 10, Workspace.Library.Modules[Module].StatModifiers.LightAttack);
        Assert.Equal(0.2, Workspace.BuildScenario().Scenario!.Defines.NNavy.COMBAT_BASE_HIT_CHANCE);
        Assert.NotEqual(0.2, Workspace.BuildScenario(withOverrides: false).Scenario!.Defines.NNavy.COMBAT_BASE_HIT_CHANCE);
        Assert.NotEqual(0.2, Workspace.BaseDefines().NNavy.COMBAT_BASE_HIT_CHANCE);

        // Unsaved edits apply at once.
        Workspace.Overrides.SetContent(entry.Set, "modules", Module, LightAttack, before + 20);
        Assert.Equal(before + 20, Workspace.Content.Modules.Single(module => module.ID == Module).StatModifiers.LightAttack);

        Workspace.UseOverrideSet(null);

        Assert.Null(Workspace.ActiveOverrides);
        Assert.Same(Workspace.Catalog, Workspace.Content);
        Assert.Equal(before, Workspace.Library.Modules[Module].StatModifiers.LightAttack);
    }

    [Fact]
    public void TheActiveSet_IsKeptInTheSettings()
    {
        var entry = Workspace.Overrides.Create("kept");
        Workspace.Overrides.Save(entry);
        Workspace.UseOverrideSet(entry.File);

        var reopened = new Workspace(new WorkspaceOptions(test.DataDirectory, []), new GuiSettingsStore(Path.Combine(test.Root, "settings.json")));

        Assert.Equal("kept", reopened.ActiveOverrides?.Set.ID);
    }

    [Fact]
    public void EditingTheActiveSet_MarksTheBattleOutdated()
    {
        var entry = Workspace.Overrides.Create("battle");
        Workspace.UseOverrideSet(entry.File);
        var runner = new BattleRunner(Workspace);

        Assert.True(runner.Start(3));
        Assert.False(runner.IsOutdated);

        Workspace.Overrides.SetDefine(entry.Set, "NNavy.COMBAT_BASE_HIT_CHANCE", 0.2);

        Assert.True(runner.IsOutdated);
    }

    [Fact]
    public async Task Batch_ComparesTheSameSeedsWithAndWithoutTheSet()
    {
        var entry = Workspace.Overrides.Create("batch");
        Workspace.Overrides.SetDefine(entry.Set, "NNavy.COMBAT_BASE_HIT_CHANCE", 0.5);
        Workspace.Scenario.MaxHours = 200;

        var plain = new BatchRunner(Workspace);
        await plain.RunAsync(3, 40, compareWithoutOverrides: true);
        Workspace.UseOverrideSet(entry.File);
        var compared = new BatchRunner(Workspace);
        await compared.RunAsync(3, 40, compareWithoutOverrides: true);

        Assert.Null(plain.Last!.Baseline);
        var baseline = compared.Last!.Baseline!;
        Assert.Equal(6, compared.Total);
        Assert.Equal(plain.Last.Runs.Select(run => (run.Seed, run.HoursFought, run.Attacker.HpLost)), baseline.Runs.Select(run => (run.Seed, run.HoursFought, run.Attacker.HpLost)));
        Assert.NotEqual(baseline.Runs.Select(run => run.Attacker.HpLost), compared.Last.Runs.Select(run => run.Attacker.HpLost));
        Assert.Equal("batch", compared.Last.Overrides?.ID);

        var rows = BatchComparison.Of(compared.Last.Runs, baseline.Runs);
        var hours = rows.Single(row => row.Label == "Hours");
        Assert.Equal(baseline.Runs.Average(run => run.HoursFought), hours.Without, 9);
        Assert.Equal(compared.Last.Runs.Average(run => run.HoursFought), hours.With, 9);
        Assert.True(hours.Change < 0);
    }

    [Fact]
    public void Info_FindsAnItemsChanges_ByPartOfTheItem()
    {
        var entry = Workspace.Overrides.Create("parts");
        const string reward = "subdoctrines[line_of_battle].rewards[flagships]";
        Workspace.Overrides.SetContent(entry.Set, "doctrines", "naval", $"{reward}.mastery", 150);
        Workspace.Overrides.SetContent(entry.Set, "doctrines", "naval", "grandDoctrines[new_fleet_in_being].effects.countryModifiers.naval_hit_chance", 0.1);

        Assert.Equal(2, OverrideInfo.ChangesOf(entry.Set, "doctrines", "naval").Count);
        Assert.Single(OverrideInfo.ChangesOf(entry.Set, "doctrines", "naval", "subdoctrines[line_of_battle]"));
        Assert.Single(OverrideInfo.ChangesOf(entry.Set, "doctrines", "naval", reward));
        Assert.Empty(OverrideInfo.ChangesOf(entry.Set, "doctrines", "naval", "subdoctrines[line_of_battle_x]"));
        Assert.Empty(OverrideInfo.ChangesOf(null, "doctrines", "naval"));

        Assert.Equal("mastery: (none) → 150", OverrideInfo.Describe(entry.Set, Workspace.Catalog, "doctrines", "naval", reward));
        Assert.Equal("effects.countryModifiers.naval_hit_chance: (none) → 0.1",
            OverrideInfo.Describe(entry.Set, Workspace.Catalog, "doctrines", "naval", "grandDoctrines[new_fleet_in_being]"));

        var fields = OverrideInfo.Fields(Workspace.Catalog, "doctrines", "naval", reward);
        Assert.Contains(fields, field => field.Path == $"{reward}.mastery");
        Assert.All(fields, field => Assert.StartsWith(reward, field.Path));
        Assert.Equal("mastery", OverrideInfo.RelativePath($"{reward}.mastery", reward));
        Assert.Equal("(item)", OverrideInfo.RelativePath(reward, reward));
    }

    [Fact]
    public void Info_ShowsTheValuesBeforeTheSet()
    {
        var entry = Workspace.Overrides.Create("info");
        var before = Workspace.Catalog.Modules.Single(module => module.ID == Module).StatModifiers.LightAttack;
        Workspace.Overrides.SetContent(entry.Set, "modules", Module, LightAttack, before + 1);
        Workspace.Overrides.SetDefine(entry.Set, "NNavy.COMBAT_BASE_HIT_CHANCE", 0.2);
        Workspace.Overrides.SetStaticModifier(entry.Set, "night", "naval_hit_chance", -0.5);
        var defines = Workspace.BaseDefines();

        var content = Assert.Single(OverrideInfo.Content(entry.Set, Workspace.Catalog));
        var define = Assert.Single(OverrideInfo.Defines(entry.Set, defines));
        var modifier = Assert.Single(OverrideInfo.StaticModifiers(entry.Set, defines));

        Assert.Equal(before, OverrideValues.Number(content.Base!));
        Assert.True(content.ItemFound);
        Assert.Equal(defines.NNavy.COMBAT_BASE_HIT_CHANCE, OverrideValues.Number(define.Base!));
        Assert.Equal(defines.StaticModifiers.Get("night", "naval_hit_chance"), modifier.Base);
        Assert.Contains("NNavy.COMBAT_BASE_HIT_CHANCE", OverrideInfo.DefineKeys(defines));
        Assert.Contains(OverrideInfo.Fields(Workspace.Catalog, "modules", Module), field => field.Path == LightAttack);
        Assert.Contains(OverrideInfo.Items(Workspace.Catalog, "spirits"), item => item.Id == Workspace.Catalog.Spirits[0].ID);
    }
}
