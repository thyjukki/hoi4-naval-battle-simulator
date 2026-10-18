namespace NavySimulator.Web;

public sealed class WebOptions
{
    public string? DataDirectory { get; private set; }
    public List<string> DefineSources { get; } = [];
    public int Port { get; private set; } = 5180;
    public bool OpenBrowser { get; private set; } = true;
    public string? SettingsFile { get; private set; }
    public bool ShowHelp { get; private set; }

    public const string Usage = """
        Usage: NavySimulator.Web [options]

          --data <dir>        Data directory to edit and simulate (default: the nearest ./data folder above the
                              working directory or the executable)
          --defines <path>    Extra define source for this session, applied after the scenario's and the GUI
                              settings' sources: a .lua file or a HOI4/mod root directory. Repeatable.
          --port <n>          Port on localhost (default 5180)
          --no-browser        Do not open the browser on start
          --settings <file>   GUI settings file (default: NavySimulator/gui-settings.json in the user's local
                              application data folder)
          --help              Show this help
        """;

    public static WebOptions Parse(string[] args)
    {
        var options = new WebOptions();

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
                    options.DataDirectory = Path.GetFullPath(NextValue());
                    break;
                case "--defines":
                    options.DefineSources.Add(NextValue());
                    break;
                case "--port":
                    options.Port = int.Parse(NextValue());

                    if (options.Port is <= 0 or > 65535)
                    {
                        throw new ArgumentException("--port must be between 1 and 65535.");
                    }

                    break;
                case "--settings":
                    options.SettingsFile = Path.GetFullPath(NextValue());
                    break;
                case "--no-browser":
                    options.OpenBrowser = false;
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
