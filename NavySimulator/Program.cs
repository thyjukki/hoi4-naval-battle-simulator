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

foreach (var line in result.HourlyLog)
{
    Console.WriteLine(line);
}

Console.WriteLine();
Console.WriteLine($"Result: {result.Outcome}");
Console.WriteLine($"Hours Elapsed: {result.HoursElapsed}");
Console.WriteLine($"Attacker Ships Remaining: {result.AttackerShipsRemaining}");
Console.WriteLine($"Defender Ships Remaining: {result.DefenderShipsRemaining}");
Console.WriteLine($"Attacker Ships Retreated: {result.AttackerShipsRetreated}");
Console.WriteLine($"Defender Ships Retreated: {result.DefenderShipsRetreated}");

