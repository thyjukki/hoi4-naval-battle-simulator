using NavySimulator.GameData.Contracts;

namespace NavySimulator.GameData.Tests.Loading;

/// <summary>S-DOCTRINE and S-SPIRIT in the scenario setup: validation, warnings, and effects on the built ships.</summary>
public sealed class DoctrineSetupTests : IDisposable
{
    private readonly string data = Path.Combine(Path.GetTempPath(), "navysim-doctrine-setup-" + Guid.NewGuid().ToString("N"));

    public DoctrineSetupTests()
    {
        DataDirectoryCopy.CopyDirectory(RepositoryPaths.Data, data);
        Directory.Delete(Path.Combine(data, "doctrines"), true);
        Directory.Delete(Path.Combine(data, "spirits"), true);
        Directory.CreateDirectory(Path.Combine(data, "doctrines"));
        Directory.CreateDirectory(Path.Combine(data, "spirits"));

        File.WriteAllText(Path.Combine(data, "doctrines", "naval.json"), """
            {
              "id": "naval",
              "tracks": [ { "id": "screens" }, { "id": "capital_ships" } ],
              "grandDoctrines": [
                {
                  "id": "test_fleet",
                  "tracks": [ "screens", "capital_ships" ],
                  "effects": { "countryModifiers": { "naval_retreat_speed": 0.1, "naval_detection": 0.05 } },
                  "milestones": { "screens": { "countryModifiers": { "naval_retreat_speed": 0.5 } } }
                }
              ],
              "subdoctrines": [
                {
                  "id": "test_screens",
                  "tracks": [ "screens" ],
                  "xor": [ "other_screens" ],
                  "rewards": [
                    { "id": "drills", "effects": { "unitBonuses": { "light_cruiser": { "max_organisation": 5, "lg_attack": 0.1, "supply_consumption": -0.1 } } } },
                    { "id": "plating", "mastery": 50, "effects": { "equipmentBonuses": { "screen_ship": { "armor_value": 0.5 } } } }
                  ]
                },
                { "id": "other_screens", "tracks": [ "screens", "capital_ships" ] },
                { "id": "test_capitals", "tracks": [ "capital_ships" ] }
              ]
            }
            """);
        File.WriteAllText(Path.Combine(data, "doctrines", "air.json"), """
            {
              "id": "air",
              "tracks": [ { "id": "strike_aircraft" } ],
              "grandDoctrines": [
                {
                  "id": "test_air",
                  "tracks": [ "strike_aircraft" ],
                  "effects": {
                    "countryModifiers": { "air_nav_efficiency": 0.05, "carrier_traffic": 0.1, "air_accidents_factor": -0.1 },
                    "unitBonuses": {
                      "category_carrier_nav_bomber": { "naval_strike_attack": 0.1, "air_agility": 0.05 },
                      "category_tac_bomber": { "air_bombing": 0.1 }
                    }
                  }
                }
              ],
              "subdoctrines": []
            }
            """);
        File.WriteAllText(Path.Combine(data, "spirits", "navy-spirits.json"), """
            {
              "spirits": [
                { "id": "fleet_spirit", "category": "naval_command_spirit", "requiredDoctrines": [ [ "test_fleet" ] ] },
                { "id": "capital_spirit", "category": "naval_command_spirit", "requiredDoctrines": [ [ "test_capitals", "other_capitals" ] ] }
              ]
            }
            """);
    }

    public void Dispose()
    {
        Directory.Delete(data, true);
    }

    [Fact]
    public void UnlockedRewardsAndMilestones_ReachTheShips()
    {
        var (scenario, loader) = Build(side => side.Doctrines["naval"] = Setup("test_fleet", ("screens", "test_screens", 150)));
        var baseline = Build(_ => { }).Scenario.Attacker.Fleet.Ships[0].GetFinalStats();
        var ship = scenario.Attacker.Fleet.Ships[0];

        // Both rewards (100 + 50): organization +5 and light attack x 1.1 on the ship; armor +50 % joins the design's one
        // multiplier sum (S-STATS-DESIGN), so it is not x 1.5 on top of the modules' armor multipliers. Then the milestone.
        Assert.Equal(baseline.Organization + 5, ship.GetFinalStats().Organization, 9);
        Assert.Equal(baseline.LightAttack * 1.1, ship.GetFinalStats().LightAttack, 6);
        Assert.Equal(ship.Design.ComputeStats([new NavySimulator.Engine.Stats.ShipStats(Armor: 0.5)]).Armor, ship.GetFinalStats().Armor, 6);
        Assert.True(ship.GetFinalStats().Armor > baseline.Armor);
        Assert.Equal(0.6, scenario.Attacker.CountryModifiers.Get("naval_retreat_speed"), 9);
        Assert.Contains("naval_detection", scenario.Attacker.IgnoredEffects());
        Assert.Contains("supply_consumption (light_cruiser)", scenario.Attacker.IgnoredEffects());
        Assert.DoesNotContain(loader.Warnings, warning => warning.StartsWith("attacker", StringComparison.Ordinal));
    }

    [Fact]
    public void AirDoctrines_RaiseThePlanesOfTheirCategories_AndTheirCountryModifiersApply()
    {
        // A-STATS: a block for category_carrier_nav_bomber reaches carrier naval bombers (sub-unit cv_nav_bomber) as a
        // factor on the equipment stat, and not land naval bombers; air_bombing has no naval counterpart.
        var (scenario, _) = Build(side => side.Doctrines["air"] = Setup("test_air"));
        var carrier = scenario.PlanesByID["cv_nav_bomber_equipment_2"];
        var land = scenario.PlanesByID["nav_bomber_equipment_2"];

        Assert.Equal(NavySimulator.Engine.Stats.FixedPoint.Truncate(1.1 * carrier.Stats.NavalAttack), scenario.Attacker.PlaneStatsOf(carrier).NavalAttack, 9);
        Assert.Equal(NavySimulator.Engine.Stats.FixedPoint.Truncate(1.05 * carrier.Stats.Agility), scenario.Attacker.PlaneStatsOf(carrier).Agility, 9);
        Assert.Equal(land.Stats.NavalAttack, scenario.Attacker.PlaneStatsOf(land).NavalAttack, 9);
        Assert.Equal(0.05, scenario.Attacker.CountryModifiers.Get("air_nav_efficiency"), 9);
        Assert.Contains("air_bombing (category_tac_bomber)", scenario.Attacker.IgnoredEffects());
        Assert.Contains("air_accidents_factor", scenario.Attacker.IgnoredEffects());
        Assert.DoesNotContain("carrier_traffic", scenario.Attacker.IgnoredEffects());
        Assert.DoesNotContain(scenario.Attacker.IgnoredEffects(), effect => effect.Contains("naval_strike_attack", StringComparison.Ordinal));
    }

    [Fact]
    public void LockedRewards_AndTheMilestoneOfAnIncompleteTrack_DoNotApply()
    {
        var (scenario, _) = Build(side => side.Doctrines["naval"] = Setup("test_fleet", ("screens", "test_screens", 120)));
        var baseline = Build(_ => { }).Scenario.Attacker.Fleet.Ships[0].GetFinalStats();

        Assert.Equal(baseline.Organization + 5, scenario.Attacker.Fleet.Ships[0].GetFinalStats().Organization, 9);
        Assert.Equal(baseline.Armor, scenario.Attacker.Fleet.Ships[0].GetFinalStats().Armor, 9);
        Assert.Equal(0.1, scenario.Attacker.CountryModifiers.Get("naval_retreat_speed"), 9);
    }

    [Fact]
    public void UnknownOrMisplacedDoctrines_AreErrors()
    {
        var exception = Assert.Throws<SetupValidationException>(() => Build(side =>
        {
            side.Doctrines["naval"] = Setup("test_fleet", ("screens", "test_capitals", 0), ("submarines", "missing", -1));
            side.Doctrines["air"] = Setup("no_such_doctrine");
        }));

        Assert.Contains("battleScenario.attacker.doctrines.naval.tracks.screens: subdoctrine 'test_capitals' belongs to track capital_ships.", exception.Errors);
        Assert.Contains(exception.Errors, error => error.StartsWith("battleScenario.attacker.doctrines.naval.tracks: 'test_fleet' has no track 'submarines'", StringComparison.Ordinal));
        Assert.Contains("battleScenario.attacker.doctrines.naval.tracks.submarines.subdoctrine references unknown subdoctrine 'missing'.", exception.Errors);
        Assert.Contains("battleScenario.attacker.doctrines.naval.tracks.submarines.mastery must be >= 0.", exception.Errors);
        Assert.Contains(exception.Errors, error => error.StartsWith("battleScenario.attacker.doctrines.air.grandDoctrine references unknown grand doctrine 'no_such_doctrine'", StringComparison.Ordinal));
    }

    [Fact]
    public void SetupsTheGameForbids_AreWarnings()
    {
        var (_, loader) = Build(side =>
        {
            side.Doctrines["naval"] = Setup("test_fleet", ("screens", "test_screens", 0), ("capital_ships", "other_screens", 0));
            side.SpiritIDs.AddRange(["fleet_spirit", "capital_spirit"]);
        });

        Assert.Contains("attacker: subdoctrines 'other_screens' and 'test_screens' exclude each other (xor) in the game.", loader.Warnings);
        Assert.Contains("attacker: spirit 'capital_spirit' needs doctrine test_capitals or other_capitals; it is simulated anyway.", loader.Warnings);
        Assert.Contains("attacker: 2 spirits in the 'naval_command_spirit' slot (fleet_spirit, capital_spirit); the game allows one.", loader.Warnings);
        Assert.DoesNotContain(loader.Warnings, warning => warning.Contains("'fleet_spirit' needs", StringComparison.Ordinal));
    }

    private (NavySimulator.Engine.Battles.BattleScenario Scenario, SetupLoader Loader) Build(Action<BattleParticipantDto> edit)
    {
        var loader = new SetupLoader();
        var catalog = loader.ReadCatalog(data);
        var scenario = SetupLoader.ReadScenario(data, "battle-scenario.json");
        edit(scenario.Attacker);
        return (loader.BuildScenario(catalog, scenario), loader);
    }

    private static DoctrineSetupDto Setup(string grandDoctrine, params (string Track, string Subdoctrine, double Mastery)[] tracks)
    {
        return new DoctrineSetupDto
        {
            GrandDoctrine = grandDoctrine,
            Tracks = tracks.ToDictionary(track => track.Track, track => new TrackSetupDto { Subdoctrine = track.Subdoctrine, Mastery = track.Mastery })
        };
    }
}
