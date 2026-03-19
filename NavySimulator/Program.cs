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

if (loader.Warnings.Count > 0)
{
    Console.WriteLine("Setup warnings:");

    foreach (var warning in loader.Warnings)
    {
        Console.WriteLine($"- {warning}");
    }

    Console.WriteLine();
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
Console.WriteLine("Pre-Simulation Fleet Overview");
PrintFleetPreview("Attacker", scenario.Attacker.Fleet);
PrintFleetPreview("Defender", scenario.Defender.Fleet);

Console.WriteLine();
Console.WriteLine("Simulation (screening + targeting + cooldowns + hit chance)");

var simulator = new BattleSimulator();
var result = simulator.Simulate(scenario);

var outputDirectoryPath = BuildRunOutputDirectory(scenario.ID);
var hourlyLogFilePath = Path.Combine(outputDirectoryPath, "hourly-log.txt");
File.WriteAllLines(hourlyLogFilePath, result.HourlyLog);
var summaryFilePath = Path.Combine(outputDirectoryPath, "summary.txt");
var shipReportFilePath = Path.Combine(outputDirectoryPath, "ship-report.txt");

File.WriteAllLines(shipReportFilePath, BuildShipReportLines(result.ShipReports));

var topDamageDealers = result.ShipReports
    .OrderByDescending(report => report.TotalDamageDone)
    .ThenBy(report => report.ShipID)
    .Take(3)
    .Select(report => $"{report.ShipID}:{report.TotalDamageDone:F1}");

var attackerShipsWithDamage = result.ShipReports.Count(report => report.Side == "Attacker" && report.TotalDamageDone > 0);
var defenderShipsWithDamage = result.ShipReports.Count(report => report.Side == "Defender" && report.TotalDamageDone > 0);

string[] summaryLines =
[
    $"Scenario: {scenario.ID}",
    $"Terrain: {scenario.Terrain}",
    $"Weather: {scenario.Weather}",
    $"Max Hours: {scenario.MaxHours}",
    string.Empty,
    $"Result: {result.Outcome}",
    $"Hours Elapsed: {result.HoursElapsed}",
    $"Attacker Production Lost: {result.AttackerProductionLost:F1}",
    $"Defender Production Lost: {result.DefenderProductionLost:F1}",
    $"Production Loss Ratio (Attacker/Defender): {FormatRatio(result.AttackerToDefenderProductionLossRatio)}",
    $"Production Loss Ratio (Defender/Attacker): {FormatRatio(result.DefenderToAttackerProductionLossRatio)}",
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
Console.WriteLine($"Ships Dealing Damage (Attacker): {attackerShipsWithDamage}");
Console.WriteLine($"Ships Dealing Damage (Defender): {defenderShipsWithDamage}");
Console.WriteLine($"Top Damage Dealers: {string.Join(", ", topDamageDealers)}");
Console.WriteLine($"Hourly log file: {hourlyLogFilePath}");
Console.WriteLine($"Summary file: {summaryFilePath}");
Console.WriteLine($"Per-ship report file: {shipReportFilePath}");

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

static string FormatRatio(double ratio)
{
    return double.IsPositiveInfinity(ratio) ? "inf" : ratio.ToString("F2");
}

static List<string> BuildShipReportLines(List<ShipBattleReport> shipReports)
{
    var lines = new List<string>();

    foreach (var shipReport in shipReports.OrderBy(report => report.Side).ThenBy(report => report.ShipID))
    {
        lines.Add($"Ship: {shipReport.ShipID} ({shipReport.Side})");
        lines.Add($"  Sunk: {shipReport.IsSunk}");
        lines.Add($"  HP: {shipReport.CurrentHp:F1}/{shipReport.MaxHp:F1} ({shipReport.HpPercentage:P1})");
        lines.Add($"  Retreated: {shipReport.DidRetreat}");
        lines.Add($"  Attempted Retreat: {shipReport.AttemptedRetreat}");
        lines.Add($"  Attempted Retreat But Sunk: {shipReport.AttemptedRetreatButSunk}");
        lines.Add($"  Production Cost: {shipReport.ProductionCost:F1}");
        lines.Add($"  Total Damage Done: {shipReport.TotalDamageDone:F1}");

        if (shipReport.DamagedShips.Count == 0)
        {
            lines.Add("  Damaged Ships: none");
        }
        else
        {
            lines.Add("  Damaged Ships:");

            foreach (var damageEvent in shipReport.DamagedShips)
            {
                lines.Add(
                    $"    - Hour {damageEvent.HourTick}, Target {damageEvent.TargetShipID}, Weapon {damageEvent.Weapon}, Damage {damageEvent.Damage:F1}, " +
                    $"HP Damage {damageEvent.AppliedHpDamage:F1}, Org Damage {damageEvent.AppliedOrganizationDamage:F1}, Killing Blow {damageEvent.DidKillingBlow}, " +
                    $"Attacker Piercing {damageEvent.AttackerPiercing:F2}, Hit Chance {damageEvent.AttackerFinalHitChance:P1}, " +
                    $"Defender Armor {damageEvent.DefenderArmor:F2}, Speed {damageEvent.DefenderSpeed:F2}, Visibility {damageEvent.DefenderVisibility:F2}");
            }
        }

        lines.Add(string.Empty);
    }

    return lines;
}

static void PrintFleetPreview(string sideLabel, Fleet fleet)
{
    var allShips = fleet.Ships;
    var designGroups = allShips
        .GroupBy(ship => ship.Design.ID)
        .OrderBy(group => group.Key, StringComparer.Ordinal)
        .ToList();

    var roleCounts = allShips
        .GroupBy(ship => ship.Design.Hull.Role)
        .ToDictionary(group => group.Key, group => group.Count());

    var totalStats = allShips
        .Select(ship => ship.Design.GetFinalStats())
        .Aggregate(new NavySimulator.Domain.Stats.ShipStats(), (current, stats) => current.Add(stats));

    var positioningPlaceholder = 1.0;
    var screening = CalculateScreeningForPreview(roleCounts, positioningPlaceholder);

    Console.WriteLine($"{sideLabel} Fleet: {fleet.ID}");
    Console.WriteLine(
        $"  Composition: Screen {GetRoleCount(roleCounts, ShipRole.Screen)}, Capital {GetRoleCount(roleCounts, ShipRole.Capital)}, " +
        $"Carrier {GetRoleCount(roleCounts, ShipRole.Carrier)}, Submarine {GetRoleCount(roleCounts, ShipRole.Submarine)}, Convoy {GetRoleCount(roleCounts, ShipRole.Convoy)}");

    Console.WriteLine("  Ship Designs:");
    foreach (var designGroup in designGroups)
    {
        var sampleShip = designGroup.First();
        var designStats = sampleShip.Design.GetFinalStats();
        Console.WriteLine(
            $"    - {designGroup.Key} x{designGroup.Count()} [{sampleShip.Design.Hull.ID}] " +
            $"HP {designStats.Hp:F1}, Org {designStats.Organization:F1}, Speed {designStats.Speed:F1}, Armor {designStats.Armor:F1}, " +
            $"LA {designStats.LightAttack:F1}, HA {designStats.HeavyAttack:F1}, Torp {designStats.TorpedoAttack:F1}, Depth {designStats.DepthChargeAttack:F1}, AA {designStats.AntiAir:F1}");
    }

    Console.WriteLine(
        $"  Fleet Firepower: LA {totalStats.LightAttack:F1}, HA {totalStats.HeavyAttack:F1}, Torp {totalStats.TorpedoAttack:F1}, " +
        $"Depth {totalStats.DepthChargeAttack:F1}, AA {totalStats.AntiAir:F1}");
    Console.WriteLine($"  Total Production Cost: {totalStats.ProductionCost:F1}");
    Console.WriteLine($"  Positioning: placeholder {positioningPlaceholder:P0} (calculation not implemented yet)");
    Console.WriteLine($"  Screening Efficiency: {screening.ScreeningEfficiency:P0}, Carrier Screening: {screening.CarrierScreeningEfficiency:P0}");
}

static int GetRoleCount(IReadOnlyDictionary<ShipRole, int> roleCounts, ShipRole role)
{
    return roleCounts.TryGetValue(role, out var count) ? count : 0;
}

static (double ScreeningEfficiency, double CarrierScreeningEfficiency) CalculateScreeningForPreview(
    IReadOnlyDictionary<ShipRole, int> roleCounts,
    double positioning)
{
    var screens = GetRoleCount(roleCounts, ShipRole.Screen);
    var capitals = GetRoleCount(roleCounts, ShipRole.Capital);
    var carriers = GetRoleCount(roleCounts, ShipRole.Carrier);
    var convoys = GetRoleCount(roleCounts, ShipRole.Convoy);

    var contributionFactor = Hoi4Defines.PositioningBaseContribution +
                             Hoi4Defines.PositioningContributionScale * Math.Clamp(positioning, 0, 1);

    var requiredScreens =
        Hoi4Defines.SCREEN_RATIO_FOR_FULL_SCREENING_FOR_CAPITALS * (capitals + carriers) +
        Hoi4Defines.SCREEN_RATIO_FOR_FULL_SCREENING_FOR_CONVOYS * convoys;
    var screeningRatio = requiredScreens <= 0 ? 1.0 : screens * contributionFactor / requiredScreens;

    var requiredCapitals =
        Hoi4Defines.CAPITAL_RATIO_FOR_FULL_SCREENING_FOR_CARRIERS * carriers +
        Hoi4Defines.CAPITAL_RATIO_FOR_FULL_SCREENING_FOR_CONVOYS * convoys;
    var carrierRatio = requiredCapitals <= 0 ? 1.0 : capitals * contributionFactor / requiredCapitals;

    return (Math.Clamp(screeningRatio, 0, 1), Math.Clamp(carrierRatio, 0, 1));
}

