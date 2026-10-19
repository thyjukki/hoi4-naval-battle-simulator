using NavySimulator.GameData;
using NavySimulator.GameData.Contracts;
using NavySimulator.Web.Services;

namespace NavySimulator.Web.Tests;

public sealed class WorkspaceTests : IDisposable
{
    private readonly TestWorkspace test = new();

    private Workspace Workspace => test.Workspace;

    public void Dispose()
    {
        test.Dispose();
    }

    [Fact]
    public void OpensTheDefaultScenario_Unchanged()
    {
        Assert.Equal(Workspace.DefaultScenarioFile, Workspace.ScenarioFile);
        Assert.False(Workspace.ScenarioDirty);
        Assert.Contains(Workspace.DefaultScenarioFile, Workspace.ListScenarioFiles());
        Assert.DoesNotContain("hulls.json", Workspace.ListScenarioFiles());
    }

    [Fact]
    public void EditingMarksTheScenarioDirty_AndRevertRestoresIt()
    {
        var id = Workspace.Scenario.ID;
        Workspace.Scenario.ID = "edited";

        Assert.True(Workspace.ScenarioDirty);

        Workspace.RevertScenario();

        Assert.False(Workspace.ScenarioDirty);
        Assert.Equal(id, Workspace.Scenario.ID);
    }

    [Fact]
    public void SaveScenarioAs_WritesTheFile_AndIsReopenedNextTime()
    {
        Workspace.Scenario.ID = "saved_from_gui";
        Workspace.Scenario.Attacker.Aggressiveness = "EngageAtHighRisk";

        Workspace.SaveScenario("gui-saved");

        Assert.Equal("gui-saved.json", Workspace.ScenarioFile);
        Assert.False(Workspace.ScenarioDirty);
        Assert.Contains("gui-saved.json", Workspace.ListScenarioFiles());

        var reopened = new Workspace(new WorkspaceOptions(test.DataDirectory, []), new GuiSettingsStore(Path.Combine(test.Root, "settings.json")));
        Assert.Equal("gui-saved.json", reopened.ScenarioFile);
        Assert.Equal("saved_from_gui", reopened.Scenario.ID);
        Assert.Equal("EngageAtHighRisk", reopened.Scenario.Attacker.Aggressiveness);
    }

    [Fact]
    public void SaveScenario_RejectsPaths()
    {
        Assert.Throws<InvalidOperationException>(() => Workspace.SaveScenario("../outside.json"));
        Assert.Throws<InvalidOperationException>(() => Workspace.SaveScenario("ship-designs/x.json"));
    }

    [Fact]
    public void BuildScenario_ReportsErrorsInsteadOfThrowing()
    {
        Workspace.Scenario.Defender.FleetID = "missing_fleet";

        var build = Workspace.BuildScenario();

        Assert.Null(build.Scenario);
        Assert.Contains(build.Errors, error => error.Contains("missing_fleet"));
    }

    [Fact]
    public void BuildScenario_RejectsAnUnknownRepairPriority()
    {
        Workspace.Scenario.Attacker.RepairPriority = "Sometimes";

        var build = Workspace.BuildScenario();

        Assert.Contains(build.Errors, error => error.Contains("repairPriority") && error.Contains("NeverRepair"));
    }

    [Fact]
    public void SettingsDefineSources_ResolveInTheDataDirectory_AndApply()
    {
        Workspace.SetSettingsDefineSources(["defines/tfb-legacy.lua"]);

        var build = Workspace.BuildScenario();

        Assert.NotNull(build.Scenario);
        Assert.Equal(0.02, build.Scenario.Defines.NNavy.COMBAT_MIN_HIT_CHANCE);
        Assert.Equal(Path.Combine(test.DataDirectory, "defines/tfb-legacy.lua"), Workspace.ExtraDefineSources.Single());
    }

    [Fact]
    public void ShipStats_ApplyTheSidesResearches()
    {
        var design = Workspace.FindDesign("cl3_armor4_full_la")!;
        var side = new BattleParticipantDto { ResearchIDs = ["light_upgrade_1"] };

        var designStats = Workspace.DesignStats(design)!;
        var shipStats = Workspace.ShipStats(design, side)!;

        Assert.True(shipStats.LightAttack > designStats.LightAttack);
        Assert.Equal(designStats.HeavyAttack, shipStats.HeavyAttack);
    }

    [Fact]
    public void DesignStats_ReportUnknownModules()
    {
        var problems = new List<string>();
        var design = new ShipDesignDto { ID = "x", HullID = "ship_hull_light_1", Modules = { ["fixed_ship_battery_slot"] = "no_such_module" } };

        var stats = Workspace.DesignStats(design, problems);

        Assert.NotNull(stats);
        Assert.Contains(problems, problem => problem.Contains("no_such_module"));
    }

    [Fact]
    public void SaveDesign_WithANewId_RenamesItInTheFleetsThatUseIt()
    {
        var design = Workspace.Clone(Workspace.FindDesign("cl3_armor4_mix")!);
        var file = Workspace.Catalog.SourceFileOf(Workspace.FindDesign("cl3_armor4_mix")!)!;
        var users = Workspace.FleetsUsingDesign("cl3_armor4_mix");
        design.ID = "cl3_renamed";

        Workspace.SaveDesign(design, file, "cl3_armor4_mix");

        Assert.Null(Workspace.FindDesign("cl3_armor4_mix"));
        Assert.NotEmpty(users);
        Assert.Equal(users, Workspace.FleetsUsingDesign("cl3_renamed"));

        var fromDisk = new SetupLoader().ReadCatalog(test.DataDirectory);
        Assert.All(users, fleetId => Assert.Contains("cl3_renamed", fromDisk.Fleets.Single(fleet => fleet.ID == fleetId).ShipDesigns.Keys));
        Assert.NotNull(Workspace.BuildScenario().Scenario);
    }

    [Fact]
    public void SaveDesign_ToAnotherFile_MovesIt()
    {
        var design = Workspace.Clone(Workspace.FindDesign("39_battleship_balanced")!);

        Workspace.SaveDesign(design, "ship-designs/moved.json", design.ID);

        var fromDisk = new SetupLoader().ReadCatalog(test.DataDirectory);
        Assert.Equal("ship-designs/moved.json", fromDisk.SourceFileOf(fromDisk.ShipDesigns.Single(candidate => candidate.ID == design.ID)));
        Assert.Empty(fromDisk.ReadErrors);
    }

    [Fact]
    public void SaveDesign_RejectsDuplicateIds()
    {
        var design = Workspace.Clone(Workspace.FindDesign("39_battleship_balanced")!);
        design.ID = "cl3_armor4_mix";

        Assert.Throws<InvalidOperationException>(() => Workspace.SaveDesign(design, "ship-designs/battleships.json", "39_battleship_balanced"));
    }

    [Fact]
    public void DeleteDesign_IsBlockedWhileAFleetUsesIt()
    {
        var blockers = Workspace.DeleteDesign("cl3_armor4_mix");

        Assert.NotEmpty(blockers);
        Assert.NotNull(Workspace.FindDesign("cl3_armor4_mix"));
    }

    [Fact]
    public void SaveFleet_WithANewId_UpdatesTheEditedScenario()
    {
        var fleetId = Workspace.Scenario.Defender.FleetID;
        var fleet = Workspace.Clone(Workspace.FindFleet(fleetId)!);
        fleet.ID = "renamed_fleet";

        Workspace.SaveFleet(fleet, Workspace.Catalog.SourceFileOf(Workspace.FindFleet(fleetId)!)!, fleetId);

        Assert.Equal("renamed_fleet", Workspace.Scenario.Defender.FleetID);
        Assert.True(Workspace.ScenarioDirty);
        Assert.NotNull(Workspace.BuildScenario().Scenario);
    }

    [Fact]
    public void DeleteFleet_RemovesItFromDisk()
    {
        var fleet = Workspace.Catalog.Fleets.First(candidate => candidate.ID != Workspace.Scenario.Attacker.FleetID && candidate.ID != Workspace.Scenario.Defender.FleetID);

        Workspace.DeleteFleet(fleet.ID);

        Assert.DoesNotContain(new SetupLoader().ReadCatalog(test.DataDirectory).Fleets, candidate => candidate.ID == fleet.ID);
    }

    [Fact]
    public void CatalogVersion_ChangesWithContentEdits()
    {
        var before = Workspace.CatalogVersion;
        var fleet = Workspace.Clone(Workspace.Catalog.Fleets[0]);

        Workspace.SaveFleet(fleet, Workspace.Catalog.SourceFileOf(Workspace.Catalog.Fleets[0])!, fleet.ID);

        Assert.NotEqual(before, Workspace.CatalogVersion);
    }

    [Fact]
    public void OpenScenario_ReloadsContentWrittenOutsideTheGui()
    {
        WriteFleetOutsideTheGui("external_fleet");
        var scenario = Workspace.Clone(Workspace.Scenario);
        scenario.Defender.FleetID = "external_fleet";
        CatalogWriter.SaveScenario(test.DataDirectory, "external-scenario.json", scenario);

        Workspace.OpenScenario("external-scenario.json");

        Assert.NotNull(Workspace.FindFleet("external_fleet"));
        Assert.Empty(Workspace.BuildScenario().Errors);
    }

    [Fact]
    public void OpenScenario_KeepsTheCatalog_WhenOnlyTheGuiWroteIt()
    {
        var fleet = Workspace.Clone(Workspace.Catalog.Fleets[0]);
        Workspace.SaveFleet(fleet, Workspace.Catalog.SourceFileOf(Workspace.Catalog.Fleets[0])!, fleet.ID);
        var version = Workspace.CatalogVersion;

        Workspace.OpenScenario(Workspace.DefaultScenarioFile);

        Assert.Equal(version, Workspace.CatalogVersion);
    }

    [Fact]
    public void OpenScenario_StillReloads_WhenFilesChangedBeforeAGuiSave()
    {
        WriteFleetOutsideTheGui("external_fleet");
        var fleet = Workspace.Clone(Workspace.Catalog.Fleets[0]);
        Workspace.SaveFleet(fleet, Workspace.Catalog.SourceFileOf(Workspace.Catalog.Fleets[0])!, fleet.ID);

        Workspace.OpenScenario(Workspace.DefaultScenarioFile);

        Assert.NotNull(Workspace.FindFleet("external_fleet"));
    }

    private void WriteFleetOutsideTheGui(string id)
    {
        var design = Workspace.FindFleet(Workspace.Scenario.Attacker.FleetID)!.ShipDesigns.Keys.First();
        File.WriteAllText(
            Path.Combine(test.DataDirectory, "force-compositions", "external.json"),
            $$"""{ "fleets": [ { "id": "{{id}}", "shipDesigns": { "{{design}}": 3 } } ] }""");
    }
}
