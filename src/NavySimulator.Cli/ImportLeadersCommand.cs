using NavySimulator.GameData.Import;

namespace NavySimulator.Cli;

/// <summary><c>NavySimulator import-leaders</c>: reads the navy leader skills, the admiral traits and every admiral (character with a navy_leader block) from the game (and mods) into a data directory.</summary>
public static class ImportLeadersCommand
{
    public const string Name = "import-leaders";

    public const string Usage = """
        Usage: NavySimulator import-leaders --game <dir> [--mod <dir>]... [--data <dir>] [--language <name>]

          --game <dir>        HOI4 install folder
          --mod <dir>         Mod folder applied over the game, as the launcher loads it (repeatable, in load order)
          --data <dir>        Data directory to write (default: ./data). Replaces every file in leader-skills/, leader-traits/ and admirals/.
          --language <name>   Localisation language for names (default: english)
        """;

    public static int Run(string[] args)
    {
        string? game = null;
        var mods = new List<string>();
        var data = Path.Combine(Directory.GetCurrentDirectory(), "data");
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

        LeaderImport import;

        try
        {
            import = LeaderImporter.Read(new GameFileSystem(game, mods), language);
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

        var files = LeaderWriter.Write(data, import);
        var countries = import.Admirals.Select(admiral => admiral.Country).OfType<string>().Distinct(StringComparer.Ordinal).Count();
        Console.WriteLine($"Imported {import.Skills.Count} navy skills, {import.Traits.Count} admiral traits and {import.Admirals.Count} admirals of {countries} countries into {Path.GetFullPath(data)}:");
        files.ToList().ForEach(file => Console.WriteLine($"  {file}"));
        return 0;
    }
}
