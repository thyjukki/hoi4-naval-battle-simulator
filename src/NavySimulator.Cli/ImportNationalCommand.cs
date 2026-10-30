using NavySimulator.GameData.Import;

namespace NavySimulator.Cli;

/// <summary>
/// <c>NavySimulator import-national</c>: reads countries' national spirits, dynamic modifiers and focuses with naval
/// combat effects from the game (and mods) into a data directory (S-NATIONAL).
/// </summary>
public static class ImportNationalCommand
{
    public const string Name = "import-national";

    public const string Usage = """
        Usage: NavySimulator import-national --game <dir> [--mod <dir>]... [--data <dir>] [--countries <TAG,TAG>] [--language <name>]

          --game <dir>          HOI4 install folder
          --mod <dir>           Mod folder applied over the game, as the launcher loads it (repeatable, in load order)
          --data <dir>          Data directory to write (default: ./data). Replaces nations/<TAG>.json of each country.
          --countries <tags>    Country tags, comma separated (default: USA,JAP)
          --language <name>     Localisation language for names (default: english)
        """;

    public static int Run(string[] args)
    {
        string? game = null;
        var mods = new List<string>();
        var data = Path.Combine(Directory.GetCurrentDirectory(), "data");
        var countries = NationImporter.DefaultCountries.ToList();
        var language = "english";

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
                case "--game":
                    game = args[++i];
                    break;
                case "--mod":
                    mods.Add(args[++i]);
                    break;
                case "--data":
                    data = args[++i];
                    break;
                case "--countries":
                    countries = args[++i].Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToList();
                    break;
                case "--language":
                    language = args[++i];
                    break;
                default:
                    Console.Error.WriteLine($"Unknown argument '{args[i]}'.");
                    Console.Error.WriteLine(Usage);
                    return 2;
            }
        }

        if (game is null)
        {
            Console.Error.WriteLine("--game is required.");
            Console.Error.WriteLine(Usage);
            return 2;
        }

        if (!Directory.Exists(data))
        {
            Console.Error.WriteLine($"Data directory not found: {data}");
            return 1;
        }

        NationImport import;

        try
        {
            import = NationImporter.Read(new GameFileSystem(game, mods), countries, language);
        }
        catch (DirectoryNotFoundException exception)
        {
            Console.Error.WriteLine(exception.Message);
            return 1;
        }

        foreach (var warning in import.Warnings)
        {
            Console.WriteLine($"Warning: {warning}");
        }

        var files = NationWriter.Write(data, import);
        var nations = import.Nations.Select(nation =>
            $"{nation.ID} ({nation.Spirits.Count} national spirits, {nation.DynamicModifiers.Count} dynamic modifiers, {nation.Focuses.Count} focuses)");
        Console.WriteLine($"Imported {string.Join(", ", nations)} into {Path.GetFullPath(data)}:");
        files.ToList().ForEach(file => Console.WriteLine($"  {file}"));
        return 0;
    }
}
