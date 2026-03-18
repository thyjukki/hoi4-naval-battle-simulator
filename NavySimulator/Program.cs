using NavySimulator.Domain;
using NavySimulator.Domain.Battles;
using NavySimulator.Setup;

var dataDirectoryPath = Path.Combine(AppContext.BaseDirectory, "Data");

if (!Directory.Exists(dataDirectoryPath))
{
    dataDirectoryPath = Path.Combine(Directory.GetCurrentDirectory(), "Data");
}

var loader = new SetupLoader();
BattleScenario scenario;

try
{
    scenario = loader.LoadScenarioFromDirectory(dataDirectoryPath);
}
catch (SetupValidationException ex)
{
    Console.WriteLine(ex.Message);
    Environment.ExitCode = 1;
    return;
}

Console.WriteLine($"Scenario: {scenario.ID}");
Console.WriteLine($"Terrain: {scenario.Terrain}");
Console.WriteLine($"Weather: {scenario.Weather}");
Console.WriteLine($"Max Hours: {scenario.MaxHours}");
Console.WriteLine();

Console.WriteLine("Attacker Setup");
Console.WriteLine($"Fleet: {scenario.Attacker.Fleet.ID}");
Console.WriteLine($"Ships: {scenario.Attacker.Fleet.Ships.Count}");
Console.WriteLine($"Commander: {scenario.Attacker.Commander}");
Console.WriteLine($"Doctrine: {scenario.Attacker.Doctrine}");
Console.WriteLine($"Tech: {scenario.Attacker.TechnologyLevel}");
Console.WriteLine($"Nation Modifier: {scenario.Attacker.NationModifier}");
Console.WriteLine();

Console.WriteLine("Defender Setup");
Console.WriteLine($"Fleet: {scenario.Defender.Fleet.ID}");
Console.WriteLine($"Ships: {scenario.Defender.Fleet.Ships.Count}");
Console.WriteLine($"Commander: {scenario.Defender.Commander}");
Console.WriteLine($"Doctrine: {scenario.Defender.Doctrine}");
Console.WriteLine($"Tech: {scenario.Defender.TechnologyLevel}");
Console.WriteLine($"Nation Modifier: {scenario.Defender.NationModifier}");

Console.WriteLine();
Console.WriteLine("Simulation (screening + targeting + cooldowns + hit chance)");

var simulator = new BattleSimulator();
var result = simulator.Simulate(scenario);

var outputDirectoryPath = BuildRunOutputDirectory(scenario.ID);
var hourlyLogFilePath = Path.Combine(outputDirectoryPath, "hourly-log.txt");
File.WriteAllLines(hourlyLogFilePath, result.HourlyLog);
var summaryFilePath = Path.Combine(outputDirectoryPath, "summary.txt");

string[] summaryLines =
[
    $"Scenario: {scenario.ID}",
    $"Terrain: {scenario.Terrain}",
    $"Weather: {scenario.Weather}",
    $"Max Hours: {scenario.MaxHours}",
    string.Empty,
    $"Result: {result.Outcome}",
    $"Hours Elapsed: {result.HoursElapsed}",
    $"Attacker Ships Remaining: {result.AttackerShipsRemaining}",
    $"Defender Ships Remaining: {result.DefenderShipsRemaining}",
    $"Attacker Ships Retreated: {result.AttackerShipsRetreated}",
    $"Defender Ships Retreated: {result.DefenderShipsRetreated}"
];

File.WriteAllLines(summaryFilePath, summaryLines);

Console.WriteLine();
foreach (var line in summaryLines.Skip(5))
{
    Console.WriteLine(line);
}
Console.WriteLine($"Hourly log file: {hourlyLogFilePath}");
Console.WriteLine($"Summary file: {summaryFilePath}");

static string BuildRunOutputDirectory(string scenarioId)
{
    var sanitizedScenarioName = SanitizePathSegment(scenarioId);
    var runtime = DateTime.Now.ToString("yyyyMMdd_HHmmss");
    var outputDirectoryPath = Path.Combine(Directory.GetCurrentDirectory(), "output", $"{sanitizedScenarioName}_{runtime}");
    Directory.CreateDirectory(outputDirectoryPath);
    return outputDirectoryPath;
}

static string SanitizePathSegment(string segment)
{
    var invalidChars = Path.GetInvalidFileNameChars();
    var sanitizedChars = segment
        .Select(ch => invalidChars.Contains(ch) ? '_' : ch)
        .ToArray();

    var sanitized = new string(sanitizedChars).Trim();
    return string.IsNullOrWhiteSpace(sanitized) ? "scenario" : sanitized;
}

