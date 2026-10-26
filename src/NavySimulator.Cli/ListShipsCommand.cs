using System.Reflection;
using System.Text.Json;
using NavySimulator.Engine;
using NavySimulator.Engine.Battles;
using NavySimulator.Engine.Battles.Mechanics;
using NavySimulator.Engine.Defines;
using NavySimulator.Engine.Doctrines;
using NavySimulator.Engine.Stats;
using NavySimulator.GameData;
using NavySimulator.GameData.Contracts;
using NavySimulator.GameData.Import;

namespace NavySimulator.Cli;

/// <summary>
/// <c>NavySimulator list-ships</c>: the scenario's ships as JSON, per side and design, with what a game save needs to
/// hold them (hull, modules by slot, the ship type as the game's sub-unit, max HP, organization, manpower), and the
/// ship's stats after researches and doctrines for comparing with logged shot profiles, and the side's doctrines as the
/// game's save holds them (unlocked reward counts, complete tracks).
/// <c>tools/ingame/make_save.py</c> reads it to write the scenario into a save.
/// </summary>
public static class ListShipsCommand
{
    public const string Name = "list-ships";

    public const string Usage = """
        Usage: NavySimulator list-ships [--data <dir>] [--scenario <file>] [--defines <path>]... [--out <file>]

          --data <dir>        Data directory (default: ./data)
          --scenario <file>   Scenario file inside the data directory (default: battle-scenario.json)
          --defines <path>    Extra define source, as for a battle run (experience levels come from the defines)
          --out <file>        Write the JSON here instead of standard output
        """;

    public static int Run(string[] args)
    {
        var data = Path.Combine(Directory.GetCurrentDirectory(), "data");
        var scenarioFile = "battle-scenario.json";
        var defineSources = new List<string>();
        string? output = null;

        for (var i = 0; i < args.Length; i++)
        {
            if (args[i] is "--help" or "-h")
            {
                Console.WriteLine(Usage);
                return 0;
            }

            if (i + 1 >= args.Length)
            {
                Console.Error.WriteLine($"Missing value for {args[i]}.");
                Console.Error.WriteLine(Usage);
                return 2;
            }

            switch (args[i])
            {
                case "--data":
                    data = args[++i];
                    break;
                case "--scenario":
                    scenarioFile = args[++i];
                    break;
                case "--defines":
                    defineSources.Add(args[++i]);
                    break;
                case "--out":
                    output = args[++i];
                    break;
                default:
                    Console.Error.WriteLine($"Unknown argument '{args[i]}'.");
                    Console.Error.WriteLine(Usage);
                    return 2;
            }
        }

        var loader = new SetupLoader();
        ContentCatalog catalog;
        BattleScenario scenario;

        try
        {
            catalog = loader.ReadCatalog(data);
            var scenarioDto = SetupLoader.ReadScenario(data, scenarioFile);
            scenario = loader.BuildScenario(catalog, scenarioDto, defineSources);
        }
        catch (DirectoryNotFoundException exception)
        {
            Console.Error.WriteLine(exception.Message);
            return 1;
        }
        catch (SetupValidationException exception)
        {
            Console.Error.WriteLine("Setup validation failed:");

            foreach (var error in exception.Errors)
            {
                Console.Error.WriteLine($"  - {error}");
            }

            return 1;
        }

        var designsByID = catalog.ShipDesigns.ToDictionary(design => design.ID, StringComparer.Ordinal);
        var document = new
        {
            scenario = scenario.ID,
            attacker = DescribeSide(scenario.Attacker, designsByID, scenario.Defines, scenario.PlanesByID),
            defender = DescribeSide(scenario.Defender, designsByID, scenario.Defines, scenario.PlanesByID)
        };
        var json = JsonSerializer.Serialize(document, new JsonSerializerOptions { WriteIndented = true });

        if (output is null)
        {
            Console.WriteLine(json);
        }
        else
        {
            File.WriteAllText(output, json + Environment.NewLine);
        }

        return 0;
    }

    private static object DescribeSide(
        BattleParticipant side,
        IReadOnlyDictionary<string, ShipDesignDto> designsByID,
        GameDefines defines,
        IReadOnlyDictionary<string, PlaneEquipment> planes)
    {
        // A-WINGS: a plane as the save needs it: its equipment type, the sub-unit of its wing pool and its missions.
        object Plane(string id) => planes.TryGetValue(id, out var plane)
            ? new { id, subUnit = plane.SubUnit, missions = plane.Missions.Order(StringComparer.Ordinal).ToList() }
            : new { id, subUnit = (string?)null, missions = new List<string>() };

        var designs = side.Fleet.Ships
            .GroupBy(ship => ship.Design.ID)
            .Select(group =>
            {
                var ship = group.First();
                var stats = ship.GetFinalStats();
                var dto = designsByID[ship.Design.ID];
                return new
                {
                    design = ship.Design.ID,
                    count = group.Count(),
                    hull = ship.Design.Hull.ID,
                    modules = dto.Modules,
                    // S-MIO: the side's assignment, else the design's.
                    mio = side.MioOf(ship.Design)?.ID,
                    // S-STATS-MIO: its bonus by game key, as the variant's design_team_bonus holds it.
                    mioBonus = GameKeys(side.MioBonus(ship.Design)),
                    shipType = ship.Design.ShipType.ID,
                    hp = stats.Hp,
                    organization = stats.Organization,
                    manpower = ship.MaxManpower,
                    experience = ship.InitialExperience,
                    // S-DOMINANCE: one ship's naval dominance.
                    navalDominance = NavalDominanceRules.ShipDominance(ship, defines),
                    // A-WINGS: each ship's air wings, in ship order (carriers only; empty lists for carriers without).
                    carrierWings = ship.Role == ShipRole.Carrier
                        ? group.Select(carrier => side.Fleet.CarrierAirwingsByShip.GetValueOrDefault(carrier.ID) ?? [])
                            .Select(wings => wings.Select(wing => new { plane = Plane(wing.PlaneID), airwings = wing.Airwings, planes = wing.PlaneCount }).ToList())
                            .ToList()
                        : null,
                    stats
                };
            })
            .ToList();

        return new
        {
            fleet = side.Fleet.ID,
            aggressiveness = side.Aggressiveness.ToString(),
            engagementRule = (int)side.Aggressiveness,
            repairPriority = side.RepairPriority.ToString(),
            repairMode = (int)side.RepairPriority,
            researchIDs = side.Researches.Select(research => research.ID).ToList(),
            spiritIDs = side.Spirits.Select(spirit => spirit.ID).ToList(),
            // S-ADMIRAL: the set skill levels (before trait adds) and trait IDs, as a character's navy_leader block has them.
            // S-POTF: the side's pride of the fleet: the simulator's ship ID and its design (the design's first ship).
            prideOfTheFleet = side.PrideOfTheFleet is { } pride ? new { ship = pride.ID, design = pride.Design.ID } : null,
            // S-DOMINANCE-DAILY: the side's start value in the battle region, when the scenario sets one (else the ships' sum).
            navalDominanceStart = side.NavalDominanceStart,
            // A-COVERAGE: the side's air bases (make_save.py writes their level into the save and checks the other planes).
            airBases = side.AirBases.Select(setup => new { state = setup.Base.State, level = setup.Level, otherPlanes = setup.OtherPlanes, supplyRatio = setup.SupplyRatio }).ToList(),
            admiral = side.Admiral is { } admiral
                ? new { id = admiral.ID, name = admiral.Name, skills = admiral.BaseSkills, traits = admiral.Traits.Select(trait => trait.ID).ToList() }
                : null,
            // S-DOCTRINE-SAVE: the save lists a grand doctrine's tracks in its order, each with its unlocked reward count.
            doctrines = side.Doctrines.Select(setup => new
            {
                folder = setup.Folder,
                grandDoctrine = setup.GrandDoctrine.ID,
                trackOrder = setup.GrandDoctrine.Tracks,
                tracks = setup.Tracks.Select(track => new
                {
                    track = track.Track,
                    subdoctrine = track.Subdoctrine.ID,
                    mastery = track.Mastery,
                    rewards = DoctrineRules.UnlockedRewards(track.Subdoctrine, track.Mastery, defines),
                    complete = DoctrineRules.IsComplete(track, defines)
                }).ToList()
            }).ToList(),
            // Active doctrine and spirit equipment bonuses that reach the side's designs (S-STATS-BONUS).
            equipmentBonuses = side.ActiveEffects
                .SelectMany(effect => effect.Effects.StatBonuses.Where(bonus => bonus.IsEquipmentBonus))
                .Select(bonus => new
                {
                    bonus = bonus.ID,
                    designs = side.Fleet.Ships.Where(ship => bonus.AppliesTo(ship.Role, ship.Design.EquipmentBonusTypes))
                        .Select(ship => ship.Design.ID).Distinct().ToList()
                })
                .Where(bonus => bonus.designs.Count > 0)
                .ToList(),
            // S-MIO: the side's state of each MIO it set up: unlocked traits (without the initial trait) and policy.
            mios = side.Mios.Select(setup => new
            {
                id = setup.Mio.ID,
                traits = setup.UnlockedTraits.Select(trait => trait.ID).ToList(),
                policy = setup.Policy?.ID
            }).ToList(),
            // A-STANCE: the task force's carrier stance (the save's task force sortie_efficiency); null: the define's default.
            carrierStanceIndex = side.CarrierStanceIndex,
            // A-REGION-SYNC: the carrier wings fly a mission over the battle's region (null: the game decides, the attacker).
            carrierWingsOnRegionMission = side.CarrierWingsOnRegionMission,
            // A-REINFORCE, A-EXTERNAL: the side's plane stockpile and land-based planes.
            planeStockpile = side.PlaneStockpile.Select(entry => new { plane = Plane(entry.Key), count = entry.Value }).ToList(),
            landBasedAirwings = side.LandBasedAirwings.Select(entry => new { plane = Plane(entry.PlaneID), planes = entry.Planes, missionEfficiency = entry.MissionEfficiency, disruptionFactor = entry.DisruptionFactor, mission = string.IsNullOrEmpty(entry.Mission) ? "naval_bomber" : entry.Mission, airBase = entry.AirBase }).ToList(),
            designs
        };
    }

    /// <summary>The non-zero stats of <paramref name="stats"/> by the game's equipment stat key (DTO names, <c>HP</c> = <c>Hp</c>).</summary>
    private static Dictionary<string, double> GameKeys(ShipStats stats)
    {
        const BindingFlags flags = BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase;
        return ShipStatKeys.ByGameKey
            .Select(pair => (Key: pair.Key, Value: (double)typeof(ShipStats).GetProperty(pair.Value, flags)!.GetValue(stats)!))
            .Where(entry => entry.Value != 0)
            .ToDictionary(entry => entry.Key, entry => entry.Value, StringComparer.Ordinal);
    }
}
