using System.Text.Json;
using NavySimulator.Engine;
using NavySimulator.Engine.Battles;
using NavySimulator.GameData;
using NavySimulator.GameData.Contracts;

namespace NavySimulator.Cli;

/// <summary>
/// <c>NavySimulator list-ships</c>: the scenario's ships as JSON, per side and design, with what a game save needs to
/// hold them (hull, modules by slot, the ship type as the game's sub-unit, max HP, organization, manpower), and the
/// ship's stats after researches for comparing with logged shot profiles.
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
            attacker = DescribeSide(scenario.Attacker, designsByID),
            defender = DescribeSide(scenario.Defender, designsByID)
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

    private static object DescribeSide(BattleParticipant side, IReadOnlyDictionary<string, ShipDesignDto> designsByID)
    {
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
                    mio = dto.MioID,
                    shipType = ship.Design.ShipType.ID,
                    hp = stats.Hp,
                    organization = stats.Organization,
                    manpower = ship.MaxManpower,
                    experience = ship.InitialExperience,
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
            commander = side.Commander,
            doctrine = side.Doctrine,
            designs
        };
    }
}
