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
var damageBreakdownLines = BuildDamageBreakdownLines(scenario, result.ShipReports);

var summaryLines = new List<string>
{
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
};

summaryLines.Add(string.Empty);
summaryLines.AddRange(damageBreakdownLines);

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

static List<string> BuildDamageBreakdownLines(BattleScenario scenario, List<ShipBattleReport> shipReports)
{
    var shipById = scenario.Attacker.Fleet.Ships
        .Concat(scenario.Defender.Fleet.Ships)
        .ToDictionary(ship => ship.ID, ship => ship);

    var attackerEnemyHealth = scenario.Defender.Fleet.Ships.Sum(ship => ship.Design.GetFinalStats().Hp);
    var defenderEnemyHealth = scenario.Attacker.Fleet.Ships.Sum(ship => ship.Design.GetFinalStats().Hp);

    var lines = new List<string>();
    lines.AddRange(BuildSideDamageBreakdown("Attacker", "Attacker", attackerEnemyHealth, shipReports, shipById));
    lines.Add(string.Empty);
    lines.AddRange(BuildSideDamageBreakdown("Defender", "Defender", defenderEnemyHealth, shipReports, shipById));
    return lines;
}

static List<string> BuildSideDamageBreakdown(
    string sideTitle,
    string sideKey,
    double enemyHealth,
    List<ShipBattleReport> shipReports,
    IReadOnlyDictionary<string, Ship> shipById)
{
    var damageEvents = shipReports
        .Where(report => report.Side == sideKey)
        .SelectMany(report => report.DamagedShips.Select(evt => (ShooterID: report.ShipID, Event: evt)))
        .Where(entry => entry.Event.AppliedHpDamage > 0)
        .ToList();

    var totalDamage = damageEvents.Sum(entry => entry.Event.AppliedHpDamage);

    var lines = new List<string>
    {
        $"{sideTitle} damage summary",
        $"Total damage dealt {totalDamage:F1} ({FormatAsShareOfEnemyHealth(totalDamage, enemyHealth)} of enemy health)",
        "Damage dealt by types"
    };

    lines.Add("- By GUNTYPE");
    lines.AddRange(BuildBreakdownEntries(
        damageEvents,
        enemyHealth,
        entry => entry.Event.Weapon.ToString()));

    lines.Add("- By SHIPGROUP");
    lines.AddRange(BuildBreakdownEntries(
        damageEvents,
        enemyHealth,
        entry => ResolveShipGroup(entry.ShooterID, shipById)));

    lines.Add("- By SHIPTYPE");
    lines.AddRange(BuildBreakdownEntries(
        damageEvents,
        enemyHealth,
        entry => ResolveShipType(entry.ShooterID, shipById)));

    return lines;
}

static List<string> BuildBreakdownEntries(
    List<(string ShooterID, ShipDamageReportEntry Event)> damageEvents,
    double enemyHealth,
    Func<(string ShooterID, ShipDamageReportEntry Event), string> keySelector)
{
    var grouped = damageEvents
        .GroupBy(keySelector)
        .Select(group => new
        {
            Name = group.Key,
            Damage = group.Sum(entry => entry.Event.AppliedHpDamage)
        })
        .OrderByDescending(item => item.Damage)
        .ThenBy(item => item.Name, StringComparer.Ordinal)
        .ToList();

    if (grouped.Count == 0)
    {
        return ["  - none"];
    }

    return grouped
        .Select(item => $"  - {item.Name} {item.Damage:F1} ({FormatAsShareOfEnemyHealth(item.Damage, enemyHealth)})")
        .ToList();
}

static string FormatAsShareOfEnemyHealth(double damage, double enemyHealth)
{
    if (enemyHealth <= 0)
    {
        return damage <= 0 ? "0.0 %" : "inf";
    }

    return (damage / enemyHealth).ToString("P1");
}

static string ResolveShipGroup(string shooterId, IReadOnlyDictionary<string, Ship> shipById)
{
    return shipById.TryGetValue(shooterId, out var ship)
        ? GetShipGroupLabel(ship.Design.Hull.Role)
        : "Unknown";
}

static string ResolveShipType(string shooterId, IReadOnlyDictionary<string, Ship> shipById)
{
    return shipById.TryGetValue(shooterId, out var ship)
        ? ship.Design.ID
        : "Unknown";
}

static string GetShipGroupLabel(ShipRole role)
{
    return role switch
    {
        ShipRole.Screen => "Screening",
        ShipRole.Capital => "Battle line",
        ShipRole.Carrier => "Carrier",
        ShipRole.Submarine => "Submarine",
        ShipRole.Convoy => "Convoy",
        _ => "Unknown"
    };
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

