using NavySimulator.Cli;
using NavySimulator.Engine;
using NavySimulator.Engine.Battles;
using NavySimulator.GameData;

if (args.Length > 0 && args[0] == ImportShipDataCommand.Name)
{
    return ImportShipDataCommand.Run(args[1..]);
}

if (args.Length > 0 && args[0] == ListShipsCommand.Name)
{
    return ListShipsCommand.Run(args[1..]);
}

CliOptions options;

try
{
    options = CliOptions.Parse(args);
}
catch (Exception exception) when (exception is ArgumentException or FormatException)
{
    Console.Error.WriteLine(exception.Message);
    Console.Error.WriteLine(CliOptions.Usage);
    return 2;
}

if (options.ShowHelp)
{
    Console.WriteLine(CliOptions.Usage);
    return 0;
}

var dataDirectory = options.DataDirectory ?? ResolveDefaultDataDirectory();
var loader = new SetupLoader();
BattleScenario scenario;

try
{
    var defineSources = options.DefineSources.Select(source => ResolveDefinesSource(source, dataDirectory)).ToList();
    scenario = loader.LoadScenarioFromDirectory(dataDirectory, options.ScenarioFile, defineSources);
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

foreach (var warning in loader.Warnings)
{
    Console.WriteLine($"Warning: {warning}");
}

Console.WriteLine($"Scenario: {scenario.ID} (data: {dataDirectory})");
Console.WriteLine($"Defines: {string.Join(" -> ", scenario.Defines.Sources)}" +
                  (loader.DefinesReport is { } report ? $" ({report.Applied.Count} values applied, {report.IgnoredKeyCount} keys not used by the engine)" : string.Empty));
Console.WriteLine($"Rules: {scenario.Rules}");
PrintSide("Attacker", scenario.Attacker);
PrintSide("Defender", scenario.Defender);

var iterations = options.Iterations ?? Math.Max(1, scenario.Iterations);
var baseSeed = options.Seed ?? scenario.Seed ?? Random.Shared.Next();
var runDirectory = Path.Combine(options.OutputDirectory, $"{Sanitize(scenario.ID)}_{DateTime.Now:yyyyMMdd_HHmmss}");
var engine = new BattleEngine();
var results = new List<NavySimulator.Engine.Results.BattleResult>();

for (var run = 1; run <= iterations; run++)
{
    var result = engine.Simulate(scenario, baseSeed + run - 1);
    results.Add(result);
    ReportWriter.WriteRun(Path.Combine(runDirectory, $"run-{run:D3}"), result);
    Console.WriteLine(
        $"Run {run}/{iterations} (seed {result.Seed}): {result.Outcome} after {result.HoursFought} h; " +
        $"attacker sunk {result.Attacker.ShipsSunk}/{result.Attacker.ShipsAtStart}, defender sunk {result.Defender.ShipsSunk}/{result.Defender.ShipsAtStart}");
}

var aggregate = ReportWriter.BuildAggregateSummary(scenario, results);
File.WriteAllLines(Path.Combine(runDirectory, "summary.txt"), aggregate);
Console.WriteLine();
aggregate.ForEach(Console.WriteLine);
Console.WriteLine();
Console.WriteLine($"Reports written to {Path.GetFullPath(runDirectory)}");
return 0;

static string ResolveDefaultDataDirectory()
{
    var nextToExecutable = Path.Combine(AppContext.BaseDirectory, "Data");
    return Directory.Exists(nextToExecutable) ? nextToExecutable : Path.Combine(Directory.GetCurrentDirectory(), "data");
}

// Relative --defines paths resolve against the working directory first, then the data directory.
static string ResolveDefinesSource(string source, string dataDirectory)
{
    if (Path.IsPathRooted(source) || File.Exists(source) || Directory.Exists(source))
    {
        return source;
    }

    var inData = Path.Combine(dataDirectory, source);
    return File.Exists(inData) || Directory.Exists(inData) ? inData : source;
}

static void PrintSide(string label, BattleParticipant participant)
{
    var roles = participant.Fleet.Ships
        .GroupBy(ship => ship.Role)
        .OrderBy(group => group.Key)
        .Select(group => $"{group.Key} {group.Count()}");
    Console.WriteLine($"{label}: fleet {participant.Fleet.ID} ({string.Join(", ", roles)}), aggressiveness {participant.Aggressiveness}, repair priority {participant.RepairPriority}");
}

static string Sanitize(string value)
{
    var invalid = Path.GetInvalidFileNameChars();
    return new string(value.Select(character => invalid.Contains(character) ? '_' : character).ToArray());
}
