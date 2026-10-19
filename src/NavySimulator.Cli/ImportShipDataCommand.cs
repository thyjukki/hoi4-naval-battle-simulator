using NavySimulator.GameData.Import;

namespace NavySimulator.Cli;

/// <summary><c>NavySimulator import-ship-data</c>: reads hulls, modules and ship types from the game (and mods) into a data directory.</summary>
public static class ImportShipDataCommand
{
    public const string Name = "import-ship-data";

    public const string Usage = """
        Usage: NavySimulator import-ship-data --game <dir> [--mod <dir>]... [--data <dir>] [--language <name>]

          --game <dir>        HOI4 install folder
          --mod <dir>         Mod folder applied over the game, as the launcher loads it (repeatable, in load order)
          --data <dir>        Data directory to write (default: ./data). Replaces every file in hulls/, modules/
                              and ship-types/.
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

        ShipDataImport import;

        try
        {
            import = ShipDataImporter.Read(new GameFileSystem(game, mods), language);
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

        var files = ShipDataWriter.Write(data, import);
        Console.WriteLine($"Imported {import.Hulls.Count} hulls, {import.Modules.Count} modules and {import.ShipTypes.Count} ship types into {Path.GetFullPath(data)}:");
        files.ToList().ForEach(file => Console.WriteLine($"  {file}"));

        if (import.SkippedEquipment.Count > 0)
        {
            Console.WriteLine($"Skipped (not Man the Guns hulls): {string.Join(", ", import.SkippedEquipment)}");
        }

        if (import.IgnoredKeys.Count > 0)
        {
            Console.WriteLine("Script keys with no simulator stat (not imported):");

            foreach (var (key, items) in import.IgnoredKeys)
            {
                Console.WriteLine($"  {key} ({items.Count})");
            }
        }

        return 0;
    }
}
