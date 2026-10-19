using NavySimulator.Engine;
using NavySimulator.Engine.Battles;
using NavySimulator.Engine.Battles.Mechanics;

namespace NavySimulator.GameData.Tests.Loading;

public class SetupLoaderTests
{
    [Fact]
    public void LoadScenarioFromDirectory_LoadsRepositoryData()
    {
        var loader = new SetupLoader();

        var scenario = loader.LoadScenarioFromDirectory(RepositoryPaths.Data);

        Assert.NotEmpty(scenario.Attacker.Fleet.Ships);
        Assert.NotEmpty(scenario.Defender.Fleet.Ships);
        Assert.NotSame(scenario.Attacker.Fleet, scenario.Defender.Fleet);
    }

    [Fact]
    public void CatalogFiles_ListEveryFileTheCatalogIsReadFrom()
    {
        var catalog = new SetupLoader().ReadCatalog(RepositoryPaths.Data);
        IEnumerable<object> items = [.. catalog.Hulls, .. catalog.Modules, .. catalog.ShipTypes, .. catalog.Mios, .. catalog.ShipDesigns, .. catalog.Researches, .. catalog.Spirits, .. catalog.Planes, .. catalog.Fleets, .. catalog.DoctrineFolders];

        var files = SetupLoader.CatalogFiles(RepositoryPaths.Data);

        Assert.All(items, item => Assert.Contains(catalog.SourceFileOf(item), files));
        Assert.DoesNotContain("battle-scenario.json", files);
    }

    [Fact]
    public void LoadScenarioFromDirectory_AppliesExtraDefineSources()
    {
        var loader = new SetupLoader();
        var tfb = Path.Combine(RepositoryPaths.Data, "defines", "tfb-legacy.lua");

        var scenario = loader.LoadScenarioFromDirectory(RepositoryPaths.Data, extraDefineSources: [tfb]);

        Assert.Equal(0.02, scenario.Defines.NNavy.COMBAT_MIN_HIT_CHANCE);
        Assert.Contains(tfb, scenario.Defines.Sources);
    }

    [Fact]
    public void LoadedScenario_RunsInTheEngine()
    {
        var scenario = new SetupLoader().LoadScenarioFromDirectory(RepositoryPaths.Data);

        var result = new BattleEngine().Simulate(scenario, 1);

        Assert.True(result.HoursFought > 0);
        Assert.NotEmpty(result.Shots);
    }

    [Fact]
    public void ReadCatalog_ReadsCountryModifiersAndWarnsAboutTheOnesNotSimulated()
    {
        var dataDirectory = Path.Combine(Path.GetTempPath(), "navysim-loader-" + Guid.NewGuid().ToString("N"));
        DataDirectoryCopy.CopyDirectory(RepositoryPaths.Data, dataDirectory);

        try
        {
            File.WriteAllText(Path.Combine(dataDirectory, "researches", "country.json"), """
                {
                  "researches": [
                    { "id": "test_doctrine", "countryModifiers": { "naval_retreat_speed": 0.1, "naval_mines_damage_factor": 0.05 } }
                  ]
                }
                """);

            var catalog = new SetupLoader().ReadCatalog(dataDirectory);
            var research = ContentLibrary.From(catalog).Researches["test_doctrine"];

            Assert.Equal(0.1, research.CountryModifiers.Get("naval_retreat_speed"), 9);
            Assert.Contains("researches/country.json 'test_doctrine': country modifier 'naval_mines_damage_factor' is not simulated yet and is ignored.", catalog.Warnings);
            Assert.DoesNotContain(catalog.Warnings, warning => warning.Contains("'naval_retreat_speed'"));
        }
        finally
        {
            Directory.Delete(dataDirectory, true);
        }
    }

    [Fact]
    public void Cl3Scenario_SideDominance_IsTheGamesRegionDominance()
    {
        // S-DOMINANCE [CAL]: the CL3 battle's save holds the Black Sea's dominance of the two task forces after their
        // patrol: BUL 17376.9375 and ROM 17238.
        var scenario = new SetupLoader().LoadScenarioFromDirectory(RepositoryPaths.Data, "battle-scenario-la-vs-torp-cl3.json");

        Assert.Equal(17376.9375, NavalDominanceRules.SideDominance(scenario.Attacker.Fleet.Ships, scenario.Defines), 2);
        Assert.Equal(17238, NavalDominanceRules.SideDominance(scenario.Defender.Fleet.Ships, scenario.Defines), 2);
    }

    [Fact]
    public void Cl3Scenario_GivesEveryShipOfBothSidesTheSmokeGeneratorsRetreatSpeed()
    {
        // TFB's smoke_generator (naval_retreat_speed = 0.2), which both countries had in the recorded battle.
        var scenario = new SetupLoader().LoadScenarioFromDirectory(RepositoryPaths.Data, "battle-scenario-la-vs-torp-cl3.json");

        Assert.All(scenario.Attacker.Fleet.Ships.Concat(scenario.Defender.Fleet.Ships),
            ship => Assert.Equal(0.2, ship.CountryModifiers.Get("naval_retreat_speed"), 9));
    }

    [Fact]
    public void RepositoryDesigns_FollowTheDesignerRulesOrOverrideThem()
    {
        var catalog = new SetupLoader().ReadCatalog(RepositoryPaths.Data);

        Assert.DoesNotContain(catalog.Warnings, warning => warning.StartsWith("ship-designs/", StringComparison.Ordinal));
    }

    [Fact]
    public void RepositoryDesigns_GetTheirShipTypeFromHullAndModules()
    {
        var catalog = new SetupLoader().ReadCatalog(RepositoryPaths.Data);
        var library = ContentLibrary.From(catalog);

        string TypeOf(string id) => library.TryCreateDesign(catalog.ShipDesigns.Single(design => design.ID == id))!.ShipType.ID;

        Assert.Equal("light_cruiser", TypeOf("cl3_armor4_full_la"));
        Assert.Equal("heavy_cruiser", TypeOf("ca3_armor4"));
        Assert.Equal("battleship", TypeOf("39_battleship_balanced"));
        Assert.Equal("battle_cruiser", TypeOf("39_battlecruiser_balanced"));
        Assert.Equal("destroyer", TypeOf("dd_3_la"));
        Assert.Equal("carrier", TypeOf("39_carrier"));
    }

    [Fact]
    public void CarrierLoadouts_GoToTheNthShipOfTheDesign_AndSidesKeepLandBasedPlanesAndStockpiles()
    {
        var dataDirectory = Path.Combine(Path.GetTempPath(), "navysim-loader-" + Guid.NewGuid().ToString("N"));
        DataDirectoryCopy.CopyDirectory(RepositoryPaths.Data, dataDirectory);

        try
        {
            File.WriteAllText(Path.Combine(dataDirectory, "force-compositions", "zz-test.json"), """
                {
                  "fleets": [
                    { "id": "zz_cv", "shipDesigns": { "39_carrier": 3 }, "carrierAirwings": { "39_carrier": [
                      { "wings": [ { "planeID": "cv_nav_bomber_equipment_1", "airwings": 2 } ] },
                      { "wings": [] },
                      { "wings": [ { "planeID": "cv_fighter_equipment_1", "planeCount": 12 }, { "planeID": "nav_bomber_equipment_1" } ] }
                    ] } }
                  ]
                }
                """);
            File.WriteAllText(Path.Combine(dataDirectory, "zz-scenario.json"), """
                {
                  "battleScenario": {
                    "id": "zz", "terrain": "ocean", "weather": "clear",
                    "attacker": { "fleetID": "zz_cv", "planeStockpile": { "cv_nav_bomber_equipment_1": 40 },
                      "landBasedAirwings": [ { "planeID": "nav_bomber_equipment_2", "planes": 100, "missionEfficiency": 0.8 } ] },
                    "defender": { "fleetID": "zz_cv" }
                  }
                }
                """);

            var loader = new SetupLoader();
            var scenario = loader.LoadScenarioFromDirectory(dataDirectory, "zz-scenario.json");
            var wings = scenario.Attacker.Fleet.CarrierAirwingsByShip;

            Assert.Equal(["zz_cv_39_carrier_001", "zz_cv_39_carrier_003"], wings.Keys.Order(StringComparer.Ordinal));
            Assert.Equal([new CarrierAirwingAssignment("cv_nav_bomber_equipment_1", 2, 10)], wings["zz_cv_39_carrier_001"]);
            Assert.Equal(new CarrierAirwingAssignment("cv_fighter_equipment_1", 1, 12), wings["zz_cv_39_carrier_003"][0]);
            Assert.Contains(loader.Warnings, warning => warning.Contains("'nav_bomber_equipment_1' on carrier 3 of '39_carrier', a plane that is not carrier capable"));
            Assert.Equal([new LandBasedAirwing("nav_bomber_equipment_2", 100, 0.8)], scenario.Attacker.LandBasedAirwings);
            Assert.Equal(40, scenario.Attacker.PlaneStockpile["cv_nav_bomber_equipment_1"]);
            Assert.Empty(scenario.Defender.LandBasedAirwings);
        }
        finally
        {
            Directory.Delete(dataDirectory, true);
        }
    }

    [Fact]
    public void DesignsBreakingModuleRestrictions_AreWarnedAbout_UnlessTheyOverrideThem()
    {
        var dataDirectory = Path.Combine(Path.GetTempPath(), "navysim-loader-" + Guid.NewGuid().ToString("N"));
        DataDirectoryCopy.CopyDirectory(RepositoryPaths.Data, dataDirectory);

        try
        {
            // TFB light cruiser armor forbids a medium battery.
            File.WriteAllText(Path.Combine(dataDirectory, "ship-designs", "zz-test.json"), """
                {
                  "shipDesigns": [
                    { "id": "cl_medium", "hullID": "ship_hull_cruiser_3", "modules": {
                      "fixed_ship_battery_slot": "ship_medium_battery_3", "fixed_ship_fire_control_system_slot": "ship_fire_control_system_2",
                      "fixed_ship_engine_slot": "cruiser_ship_engine_3", "fixed_ship_armor_slot": "ship_armor_cruiser_3" } },
                    { "id": "cl_medium_override", "hullID": "ship_hull_cruiser_3", "allowAnyModule": true, "modules": {
                      "fixed_ship_battery_slot": "ship_medium_battery_3", "fixed_ship_fire_control_system_slot": "ship_fire_control_system_2",
                      "fixed_ship_engine_slot": "cruiser_ship_engine_3", "fixed_ship_armor_slot": "ship_armor_cruiser_3" } }
                  ]
                }
                """);

            var catalog = new SetupLoader().ReadCatalog(dataDirectory);
            var library = ContentLibrary.From(catalog);
            var overridden = library.TryCreateDesign(catalog.ShipDesigns.Single(design => design.ID == "cl_medium_override"))!;

            var warning = Assert.Single(catalog.Warnings, warning => warning.StartsWith("ship-designs/zz-test.json", StringComparison.Ordinal));
            Assert.Contains("'cl_medium'", warning);
            Assert.Contains("'ship_armor_cruiser_3' forbids 'ship_medium_battery_3'", warning);
            Assert.Equal("light_cruiser", overridden.ShipType.ID);
        }
        finally
        {
            Directory.Delete(dataDirectory, true);
        }
    }

    [Fact]
    public void ModulesInSlotsTheHullLacks_FailTheBuild()
    {
        var dataDirectory = Path.Combine(Path.GetTempPath(), "navysim-loader-" + Guid.NewGuid().ToString("N"));
        DataDirectoryCopy.CopyDirectory(RepositoryPaths.Data, dataDirectory);

        try
        {
            File.WriteAllText(Path.Combine(dataDirectory, "ship-designs", "zz-test.json"), """
                { "shipDesigns": [ { "id": "dd_extra", "hullID": "ship_hull_light_1", "modules": { "rear_9_custom_slot": "ship_light_battery_1" } } ] }
                """);

            var exception = Assert.Throws<SetupValidationException>(
                () => new SetupLoader().LoadScenarioFromDirectory(dataDirectory));

            Assert.Contains(exception.Errors, error => error.Contains("'dd_extra'") && error.Contains("has no slot 'rear_9_custom_slot'"));
        }
        finally
        {
            Directory.Delete(dataDirectory, true);
        }
    }
}
