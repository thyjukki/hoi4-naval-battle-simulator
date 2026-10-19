namespace NavySimulator.Cli;

public sealed class CliOptions
{
    public string? DataDirectory { get; private set; }
    public string ScenarioFile { get; private set; } = "battle-scenario.json";
    public List<string> DefineSources { get; } = [];
    public int? Iterations { get; private set; }
    public int? Seed { get; private set; }
    public string OutputDirectory { get; private set; } = "output";
    public bool ShowHelp { get; private set; }

    public const string Usage = """
        Usage: NavySimulator [options]

          --data <dir>        Data directory (default: the Data folder next to the executable, then ./data)
          --scenario <file>   Scenario file inside the data directory (default: battle-scenario.json)
          --defines <path>    Extra define source applied after the scenario's: a .lua file, or a HOI4/mod root
                              directory (uses its common/defines/*.lua). Repeatable.
          --iterations <n>    Override the scenario's iteration count
          --seed <n>          Base seed; run i uses seed + i - 1 (default: scenario seed, else random)
          --out <dir>         Output root directory (default: output)
          --help              Show this help

        Import hulls, modules and ship types from the game: NavySimulator import-ship-data --help
        List a scenario's ships as JSON (for tools/ingame/make_save.py): NavySimulator list-ships --help
        Replay a battle logged in the game with its own rolls: NavySimulator replay --help
        """;

    public static CliOptions Parse(string[] args)
    {
        var options = new CliOptions();

        for (var i = 0; i < args.Length; i++)
        {
            var arg = args[i];

            string NextValue()
            {
                if (i + 1 >= args.Length)
                {
                    throw new ArgumentException($"Missing value for {arg}.");
                }

                return args[++i];
            }

            switch (arg)
            {
                case "--data":
                    options.DataDirectory = NextValue();
                    break;
                case "--scenario":
                    options.ScenarioFile = NextValue();
                    break;
                case "--defines":
                    options.DefineSources.Add(NextValue());
                    break;
                case "--iterations":
                    options.Iterations = int.Parse(NextValue());

                    if (options.Iterations <= 0)
                    {
                        throw new ArgumentException("--iterations must be at least 1.");
                    }

                    break;
                case "--seed":
                    options.Seed = int.Parse(NextValue());
                    break;
                case "--out":
                    options.OutputDirectory = NextValue();
                    break;
                case "--help" or "-h":
                    options.ShowHelp = true;
                    break;
                default:
                    throw new ArgumentException($"Unknown argument '{arg}'.");
            }
        }

        return options;
    }
}
