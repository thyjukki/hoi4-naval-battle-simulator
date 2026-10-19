using NavySimulator.Engine.Battles;
using NavySimulator.GameData.Contracts;

namespace NavySimulator.GameData.Tests.Saving;

public sealed class CatalogWriterTests : IDisposable
{
    private readonly string dataDirectory;

    public CatalogWriterTests()
    {
        dataDirectory = Path.Combine(Path.GetTempPath(), "navysim-writer-" + Guid.NewGuid().ToString("N"));
        DataDirectoryCopy.CopyDirectory(RepositoryPaths.Data, dataDirectory);
    }

    public void Dispose()
    {
        Directory.Delete(dataDirectory, true);
    }

    [Fact]
    public void ReadCatalog_RemembersTheFileOfEveryItem()
    {
        var catalog = new SetupLoader().ReadCatalog(dataDirectory);

        Assert.Empty(catalog.ReadErrors);
        Assert.All(catalog.ShipDesigns, design => Assert.StartsWith("ship-designs/", catalog.SourceFileOf(design)));
        Assert.All(catalog.Fleets, fleet => Assert.StartsWith("force-compositions/", catalog.SourceFileOf(fleet)));
        Assert.Equal("ship-designs/battleships.json", catalog.SourceFileOf(catalog.ShipDesigns.First(design => design.ID == "39_battleship_balanced")));
    }

    [Fact]
    public void BuildScenario_FromTheCatalog_MatchesLoadingFromTheDirectory()
    {
        var loader = new SetupLoader();
        var fromDirectory = loader.LoadScenarioFromDirectory(dataDirectory);
        var catalog = loader.ReadCatalog(dataDirectory);
        var fromCatalog = loader.BuildScenario(catalog, SetupLoader.ReadScenario(dataDirectory, "battle-scenario.json"));

        var expected = new BattleEngine().Simulate(fromDirectory, 3);
        var actual = new BattleEngine().Simulate(fromCatalog, 3);

        Assert.Equal(expected.Shots, actual.Shots);
        Assert.Equal(expected.Ships, actual.Ships);
    }

    [Fact]
    public void BuildScenario_UsesInMemoryEdits()
    {
        var loader = new SetupLoader();
        var catalog = loader.ReadCatalog(dataDirectory);
        var scenario = SetupLoader.ReadScenario(dataDirectory, "battle-scenario.json");
        var fleet = catalog.Fleets.First(candidate => candidate.ID == scenario.Attacker.FleetID);
        fleet.ShipDesigns = new Dictionary<string, int> { [fleet.ShipDesigns.Keys.First()] = 2 };

        var built = loader.BuildScenario(catalog, scenario);

        Assert.Equal(2, built.Attacker.Fleet.Ships.Count);
    }

    [Fact]
    public void BuildScenario_ReportsUnknownFleets()
    {
        var loader = new SetupLoader();
        var catalog = loader.ReadCatalog(dataDirectory);
        var scenario = SetupLoader.ReadScenario(dataDirectory, "battle-scenario.json");
        scenario.Defender.FleetID = "no_such_fleet";

        var exception = Assert.Throws<SetupValidationException>(() => loader.BuildScenario(catalog, scenario));

        Assert.Contains(exception.Errors, error => error.Contains("no_such_fleet"));
    }

    [Fact]
    public void SaveShipDesign_ReplacesTheEntryInPlace_AndKeepsTheOthers()
    {
        var loader = new SetupLoader();
        var before = loader.ReadCatalog(dataDirectory);
        var design = before.ShipDesigns.First(candidate => candidate.ID == "39_battleship_balanced");
        var file = before.SourceFileOf(design)!;
        var othersInFile = before.ShipDesigns.Where(candidate => before.SourceFileOf(candidate) == file).Select(candidate => candidate.ID).ToList();
        design.Modules = design.Modules.Take(3).ToDictionary();

        CatalogWriter.SaveShipDesign(dataDirectory, file, design);
        var after = loader.ReadCatalog(dataDirectory);

        Assert.Equal(othersInFile, after.ShipDesigns.Where(candidate => after.SourceFileOf(candidate) == file).Select(candidate => candidate.ID));
        Assert.Equal(design.Modules, after.ShipDesigns.Single(candidate => candidate.ID == design.ID).Modules);
        Assert.Empty(after.ReadErrors);
    }

    [Fact]
    public void SaveShipDesign_WithPreviousId_RenamesTheEntry()
    {
        var loader = new SetupLoader();
        var catalog = loader.ReadCatalog(dataDirectory);
        var design = catalog.ShipDesigns.First(candidate => candidate.ID == "39_battleship_balanced");
        var file = catalog.SourceFileOf(design)!;
        design.ID = "renamed_battleship";

        CatalogWriter.SaveShipDesign(dataDirectory, file, design, previousId: "39_battleship_balanced");
        var after = loader.ReadCatalog(dataDirectory);

        Assert.DoesNotContain(after.ShipDesigns, candidate => candidate.ID == "39_battleship_balanced");
        Assert.Contains(after.ShipDesigns, candidate => candidate.ID == "renamed_battleship");
    }

    [Fact]
    public void SaveShipDesign_ToANewFile_CreatesIt()
    {
        var design = new ShipDesignDto { ID = "custom_dd", HullID = "ship_hull_light_1" };

        CatalogWriter.SaveShipDesign(dataDirectory, "ship-designs/custom.json", design);
        var catalog = new SetupLoader().ReadCatalog(dataDirectory);
        var text = File.ReadAllText(Path.Combine(dataDirectory, "ship-designs", "custom.json"));

        Assert.Contains(catalog.ShipDesigns, candidate => candidate.ID == "custom_dd" && catalog.SourceFileOf(candidate) == "ship-designs/custom.json");
        Assert.Contains("\"$schema\": \"../schemas/ship-designs.schema.json\"", text);
        Assert.DoesNotContain("mioID", text);
        Assert.DoesNotContain("allowAnyModule", text);
        Assert.Contains("\"modules\": {}", text);
    }

    [Fact]
    public void DeleteFleet_RemovesOnlyThatFleet()
    {
        var loader = new SetupLoader();
        var catalog = loader.ReadCatalog(dataDirectory);
        var fleet = catalog.Fleets.First();
        var file = catalog.SourceFileOf(fleet)!;

        Assert.True(CatalogWriter.DeleteFleet(dataDirectory, file, fleet.ID));
        var after = loader.ReadCatalog(dataDirectory);

        Assert.DoesNotContain(after.Fleets, candidate => candidate.ID == fleet.ID);
        Assert.Equal(catalog.Fleets.Count - 1, after.Fleets.Count);
        Assert.False(CatalogWriter.DeleteFleet(dataDirectory, file, fleet.ID));
    }

    [Fact]
    public void SaveFleet_LeavesOutEmptyOptionalBlocks()
    {
        var fleet = new FleetDto { ID = "f", ShipDesigns = new Dictionary<string, int> { ["dd_3_la"] = 3 } };

        CatalogWriter.SaveFleet(dataDirectory, "force-compositions/custom.json", fleet);
        var text = File.ReadAllText(Path.Combine(dataDirectory, "force-compositions", "custom.json"));

        Assert.DoesNotContain("shipExperienceLevels", text);
        Assert.DoesNotContain("carrierAirwings", text);
        Assert.Contains("\"dd_3_la\": 3", text);
    }

    [Fact]
    public void SaveScenario_RoundTrips()
    {
        var scenario = SetupLoader.ReadScenario(dataDirectory, "battle-scenario.json");
        scenario.ID = "round_trip";
        scenario.Seed = 99;
        scenario.Rules = new BattleRulesDto { NightEnabled = false, WeatherEnabled = false, StartHourOfDay = 6 };
        scenario.Attacker.Aggressiveness = "EngageAtHighRisk";
        scenario.Attacker.RepairPriority = "NeverRepair";

        CatalogWriter.SaveScenario(dataDirectory, "round-trip.json", scenario);
        var loaded = SetupLoader.ReadScenario(dataDirectory, "round-trip.json");
        var built = new SetupLoader().LoadScenarioFromDirectory(dataDirectory, "round-trip.json");

        Assert.Equal("round_trip", loaded.ID);
        Assert.Equal(99, loaded.Seed);
        Assert.Equal(false, loaded.Rules!.NightEnabled);
        Assert.Equal(6, built.Rules.StartHourOfDay);
        Assert.False(built.Rules.WeatherEnabled);
        Assert.Equal(Aggressiveness.EngageAtHighRisk, built.Attacker.Aggressiveness);
        Assert.Equal(RepairPriority.NeverRepair, built.Attacker.RepairPriority);
        Assert.Equal(RepairPriority.High, built.Defender.RepairPriority);
        Assert.Equal(scenario.Attacker.ResearchIDs, loaded.Attacker.ResearchIDs);
    }

    [Fact]
    public void Writer_RejectsPathsOutsideTheDataDirectory()
    {
        var design = new ShipDesignDto { ID = "x", HullID = "ship_hull_light_1" };

        Assert.Throws<ArgumentException>(() => CatalogWriter.SaveShipDesign(dataDirectory, "../escape.json", design));
        Assert.Throws<ArgumentException>(() => CatalogWriter.SaveShipDesign(dataDirectory, "ship-designs/not-json.txt", design));
    }
}
