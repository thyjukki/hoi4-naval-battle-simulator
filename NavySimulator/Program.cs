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
Console.WriteLine();

Console.WriteLine("Defender Setup");
Console.WriteLine($"Fleet: {scenario.Defender.Fleet.ID}");
Console.WriteLine($"Ships: {scenario.Defender.Fleet.Ships.Count}");
Console.WriteLine($"Commander: {scenario.Defender.Commander}");
Console.WriteLine($"Doctrine: {scenario.Defender.Doctrine}");

var iterations = scenario.Iterations <= 0 ? 1 : scenario.Iterations;
Console.WriteLine($"Iterations: {iterations}");

var simulator = new BattleSimulator();
var outputDirectoryPath = BuildRunOutputDirectory(scenario.ID);
var iterationResults = new List<BattleResult>();
var iterationScenarios = new List<BattleScenario>();

for (var runNumber = 1; runNumber <= iterations; runNumber++)
{
    var runScenario = runNumber == 1 ? scenario : loader.LoadScenarioFromDirectory(dataDirectoryPath);
    iterationScenarios.Add(runScenario);

    Console.WriteLine();
    Console.WriteLine($"Iteration {runNumber}/{iterations}");
    Console.WriteLine("Pre-Simulation Fleet Overview");
    PrintFleetPreview("Attacker", runScenario.Attacker.Fleet, runScenario.Defender.Fleet);
    PrintFleetPreview("Defender", runScenario.Defender.Fleet, runScenario.Attacker.Fleet);

    Console.WriteLine();
    Console.WriteLine("Simulation (screening + targeting + cooldowns + hit chance)");

    var result = simulator.Simulate(runScenario);
    iterationResults.Add(result);

    var fileSuffix = iterations > 1 ? $"-RUN{runNumber}" : string.Empty;
    var hourlyLogFilePath = Path.Combine(outputDirectoryPath, $"hourly-log{fileSuffix}.txt");
    var summaryFilePath = Path.Combine(outputDirectoryPath, $"summary{fileSuffix}.txt");
    var shipReportFilePath = Path.Combine(outputDirectoryPath, $"ship-report{fileSuffix}.txt");

    File.WriteAllLines(hourlyLogFilePath, result.HourlyLog);
    File.WriteAllLines(shipReportFilePath, BuildShipReportLines(result.ShipReports));

    var topDamageDealers = result.ShipReports
        .OrderByDescending(report => report.TotalDamageDone)
        .ThenBy(report => report.ShipID)
        .Take(3)
        .Select(report => $"{report.ShipID}:{report.TotalDamageDone:F1}");

    var attackerShipsWithDamage = result.ShipReports.Count(report => report.Side == "Attacker" && report.TotalDamageDone > 0);
    var defenderShipsWithDamage = result.ShipReports.Count(report => report.Side == "Defender" && report.TotalDamageDone > 0);
    var damageBreakdownLines = BuildDamageBreakdownLines(runScenario, result.ShipReports);

    var summaryLines = new List<string>
    {
        $"Scenario: {runScenario.ID}",
        $"Terrain: {runScenario.Terrain}",
        $"Weather: {runScenario.Weather}",
        $"Max Hours: {runScenario.MaxHours}",
        $"Iteration: {runNumber}/{iterations}",
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
        $"Defender Ships Retreated: {result.DefenderShipsRetreated}",
        $"Attacker Planes (Start/Lost/Remaining): {FormatPlaneStrength(result.AttackerPlanesAtStart)} / {FormatPlaneStrength(result.AttackerPlanesLost)} / {FormatPlaneStrength(GetRemainingPlaneStrength(result.AttackerPlanesAtStart, result.AttackerPlanesLost))}",
        $"Defender Planes (Start/Lost/Remaining): {FormatPlaneStrength(result.DefenderPlanesAtStart)} / {FormatPlaneStrength(result.DefenderPlanesLost)} / {FormatPlaneStrength(GetRemainingPlaneStrength(result.DefenderPlanesAtStart, result.DefenderPlanesLost))}"
    };

    summaryLines.Add(string.Empty);
    summaryLines.AddRange(damageBreakdownLines);
    File.WriteAllLines(summaryFilePath, summaryLines);

    Console.WriteLine();
    foreach (var line in summaryLines.Skip(6))
    {
        Console.WriteLine(line);
    }

    Console.WriteLine($"Ships Dealing Damage (Attacker): {attackerShipsWithDamage}");
    Console.WriteLine($"Ships Dealing Damage (Defender): {defenderShipsWithDamage}");
    Console.WriteLine($"Top Damage Dealers: {string.Join(", ", topDamageDealers)}");
    Console.WriteLine($"Hourly log file: {hourlyLogFilePath}");
    Console.WriteLine($"Summary file: {summaryFilePath}");
    Console.WriteLine($"Per-ship report file: {shipReportFilePath}");
}

if (iterations > 1)
{
    var averagesSummaryLines = BuildIterationsAverageSummaryLines(iterationScenarios, iterationResults);
    var averagesSummaryFilePath = Path.Combine(outputDirectoryPath, "summary-averages.txt");
    File.WriteAllLines(averagesSummaryFilePath, averagesSummaryLines);

    Console.WriteLine();
    foreach (var line in averagesSummaryLines)
    {
        Console.WriteLine(line);
    }
    Console.WriteLine($"Averages summary file: {averagesSummaryFilePath}");
}

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

    var attackerEnemyHealth = scenario.Defender.Fleet.Ships.Sum(ship => ship.GetFinalStats().Hp);
    var defenderEnemyHealth = scenario.Attacker.Fleet.Ships.Sum(ship => ship.GetFinalStats().Hp);

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

static void PrintFleetPreview(string sideLabel, Fleet fleet, Fleet opposingFleet)
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
        .Select(ship => ship.GetFinalStats())
        .Aggregate(new NavySimulator.Domain.Stats.ShipStats(), (current, stats) => current.Add(stats));

    var positioning = CalculateFleetSizePositioningForPreview(allShips.Count, opposingFleet.Ships.Count);
    var screening = CalculateScreeningForPreview(roleCounts, positioning);
    var planeStrength = GetFleetPlaneStrengthForPreview(fleet);

    Console.WriteLine($"{sideLabel} Fleet: {fleet.ID}");
    Console.WriteLine(
        $"  Composition: Screen {GetRoleCount(roleCounts, ShipRole.Screen)}, Capital {GetRoleCount(roleCounts, ShipRole.Capital)}, " +
        $"Carrier {GetRoleCount(roleCounts, ShipRole.Carrier)}, Submarine {GetRoleCount(roleCounts, ShipRole.Submarine)}, Convoy {GetRoleCount(roleCounts, ShipRole.Convoy)}");

    Console.WriteLine("  Ship Designs:");
    foreach (var designGroup in designGroups)
    {
        var sampleShip = designGroup.First();
        var designStats = sampleShip.GetFinalStats();
        Console.WriteLine(
            $"    - {designGroup.Key} x{designGroup.Count()} [{sampleShip.Design.Hull.ID}] " +
            $"HP {designStats.Hp:F1}, Org {designStats.Organization:F1}, Speed {designStats.Speed:F1}, Armor {designStats.Armor:F1}, " +
            $"LA {designStats.LightAttack:F1}, HA {designStats.HeavyAttack:F1}, Torp {designStats.TorpedoAttack:F1}, Depth {designStats.DepthChargeAttack:F1}, AA {designStats.AntiAir:F1}");
    }

    Console.WriteLine(
        $"  Fleet Firepower: LA {totalStats.LightAttack:F1}, HA {totalStats.HeavyAttack:F1}, Torp {totalStats.TorpedoAttack:F1}, " +
        $"Depth {totalStats.DepthChargeAttack:F1}, AA {totalStats.AntiAir:F1}");

    if (fleet.CarrierAirwingsByShipDesign.Count > 0)
    {
        Console.WriteLine("  Carrier Airwings:");

        foreach (var designEntry in fleet.CarrierAirwingsByShipDesign.OrderBy(entry => entry.Key, StringComparer.Ordinal))
        {
            var assignmentSummary = string.Join(", ",
                designEntry.Value
                    .OrderBy(assignment => assignment.Type)
                    .ThenBy(assignment => assignment.PlaneID, StringComparer.Ordinal)
                    .Select(assignment =>
                        $"{assignment.Type}:{assignment.Airwings}w ({assignment.PlaneCount} planes) [{assignment.PlaneID}]"));

            Console.WriteLine($"    - {designEntry.Key}: {assignmentSummary}");
        }
    }

    Console.WriteLine($"  Carrier Planes: {FormatPlaneStrength(planeStrength)}");
    Console.WriteLine($"  Total Production Cost: {totalStats.ProductionCost:F1}");
    Console.WriteLine($"  Positioning: {positioning:P0}");
    Console.WriteLine($"  Screening Efficiency: {screening.ScreeningEfficiency:P0}, Carrier Screening: {screening.CarrierScreeningEfficiency:P0}");
}

static PlaneStrength GetFleetPlaneStrengthForPreview(Fleet fleet)
{
    var carrierCountByDesign = fleet.Ships
        .Where(ship => ship.Design.Hull.Role == ShipRole.Carrier)
        .GroupBy(ship => ship.Design.ID)
        .ToDictionary(group => group.Key, group => group.Count(), StringComparer.Ordinal);

    var fighterPlanes = 0;
    var bomberPlanes = 0;

    foreach (var carrierGroup in carrierCountByDesign)
    {
        if (!fleet.CarrierAirwingsByShipDesign.TryGetValue(carrierGroup.Key, out var assignments))
        {
            continue;
        }

        var carrierCount = carrierGroup.Value;
        fighterPlanes += carrierCount * assignments
            .Where(assignment => assignment.Type == AirwingType.Fighter)
            .Sum(assignment => assignment.PlaneCount);
        bomberPlanes += carrierCount * assignments
            .Where(assignment => assignment.Type == AirwingType.Bomber)
            .Sum(assignment => assignment.PlaneCount);
    }

    return new PlaneStrength(fighterPlanes, bomberPlanes);
}

static PlaneStrength GetRemainingPlaneStrength(PlaneStrength atStart, PlaneStrength lost)
{
    return new PlaneStrength(
        Math.Max(0, atStart.Fighters - lost.Fighters),
        Math.Max(0, atStart.Bombers - lost.Bombers));
}

static string FormatPlaneStrength(PlaneStrength strength)
{
    return $"Fighters {strength.Fighters}, Bombers {strength.Bombers}, Total {strength.Total}";
}

static double CalculateFleetSizePositioningForPreview(int ownShipCount, int opponentShipCount)
{
    if (ownShipCount < Hoi4Defines.MIN_SHIPS_FOR_HIGHER_SHIP_RATIO_PENALTY ||
        ownShipCount <= opponentShipCount)
    {
        return Hoi4Defines.BASE_POSITIONING;
    }

    var shipRatio = ownShipCount / (double)opponentShipCount;
    var ratioAboveParity = Math.Max(0, shipRatio - 1.0);
    var penalty = Math.Min(
        Hoi4Defines.MAX_POSITIONING_PENALTY_FROM_HIGHER_SHIP_RATIO,
        ratioAboveParity * Hoi4Defines.HIGHER_SHIP_RATIO_POSITIONING_PENALTY_FACTOR);

    return Math.Clamp(Hoi4Defines.BASE_POSITIONING - penalty, 0, 1);
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

static List<string> BuildIterationsAverageSummaryLines(
    List<BattleScenario> iterationScenarios,
    List<BattleResult> iterationResults)
{
    var lines = new List<string>
    {
        "Iterations averages summary",
        $"Runs: {iterationResults.Count}"
    };

    var outcomeCounts = iterationResults
        .GroupBy(result => result.Outcome)
        .OrderBy(group => group.Key, StringComparer.Ordinal)
        .Select(group => $"{group.Key}:{group.Count()}");
    lines.Add($"Outcome distribution: {string.Join(", ", outcomeCounts)}");
    lines.Add($"Avg Hours Elapsed: {iterationResults.Average(result => result.HoursElapsed):F1}");
    lines.Add($"Avg Attacker Production Lost: {iterationResults.Average(result => result.AttackerProductionLost):F1}");
    lines.Add($"Avg Defender Production Lost: {iterationResults.Average(result => result.DefenderProductionLost):F1}");
    lines.Add($"Avg Attacker Ships Remaining: {iterationResults.Average(result => result.AttackerShipsRemaining):F2}");
    lines.Add($"Avg Defender Ships Remaining: {iterationResults.Average(result => result.DefenderShipsRemaining):F2}");
    lines.Add($"Avg Attacker Ships Retreated: {iterationResults.Average(result => result.AttackerShipsRetreated):F2}");
    lines.Add($"Avg Defender Ships Retreated: {iterationResults.Average(result => result.DefenderShipsRetreated):F2}");
    lines.Add($"Avg Attacker Planes Lost: Fighters {iterationResults.Average(result => result.AttackerPlanesLost.Fighters):F2}, Bombers {iterationResults.Average(result => result.AttackerPlanesLost.Bombers):F2}, Total {iterationResults.Average(result => result.AttackerPlanesLost.Total):F2}");
    lines.Add($"Avg Defender Planes Lost: Fighters {iterationResults.Average(result => result.DefenderPlanesLost.Fighters):F2}, Bombers {iterationResults.Average(result => result.DefenderPlanesLost.Bombers):F2}, Total {iterationResults.Average(result => result.DefenderPlanesLost.Total):F2}");

    var attackerDamagePercentages = new List<double>();
    var defenderDamagePercentages = new List<double>();

    for (var i = 0; i < iterationResults.Count; i++)
    {
        var result = iterationResults[i];
        var scenario = iterationScenarios[i];

        var attackerDamage = CalculateTotalAppliedHpDamage(result.ShipReports, "Attacker");
        var defenderDamage = CalculateTotalAppliedHpDamage(result.ShipReports, "Defender");

        var attackerEnemyHealth = scenario.Defender.Fleet.Ships.Sum(ship => ship.GetFinalStats().Hp);
        var defenderEnemyHealth = scenario.Attacker.Fleet.Ships.Sum(ship => ship.GetFinalStats().Hp);

        attackerDamagePercentages.Add(attackerEnemyHealth <= 0 ? 0 : attackerDamage / attackerEnemyHealth);
        defenderDamagePercentages.Add(defenderEnemyHealth <= 0 ? 0 : defenderDamage / defenderEnemyHealth);
    }

    lines.Add($"Avg Attacker Damage (% enemy health): {(attackerDamagePercentages.Average()):P1}");
    lines.Add($"Avg Defender Damage (% enemy health): {(defenderDamagePercentages.Average()):P1}");

    lines.Add(string.Empty);
    lines.Add("Avg ship types lost (Attacker)");
    lines.AddRange(BuildAverageShipTypeLossLines(iterationScenarios, iterationResults, "Attacker"));
    lines.Add(string.Empty);
    lines.Add("Avg ship types lost (Defender)");
    lines.AddRange(BuildAverageShipTypeLossLines(iterationScenarios, iterationResults, "Defender"));

    return lines;
}

static double CalculateTotalAppliedHpDamage(List<ShipBattleReport> shipReports, string side)
{
    return shipReports
        .Where(report => report.Side == side)
        .SelectMany(report => report.DamagedShips)
        .Sum(damageEvent => damageEvent.AppliedHpDamage);
}

static List<string> BuildAverageShipTypeLossLines(
    List<BattleScenario> iterationScenarios,
    List<BattleResult> iterationResults,
    string side)
{
    var allDesignIds = new SortedSet<string>(StringComparer.Ordinal);
    var initialCountsByDesign = new Dictionary<string, int>(StringComparer.Ordinal);
    var totalLossesByDesign = new Dictionary<string, double>(StringComparer.Ordinal);

    for (var i = 0; i < iterationResults.Count; i++)
    {
        var scenario = iterationScenarios[i];
        var result = iterationResults[i];

        var scenarioShips = side == "Attacker" ? scenario.Attacker.Fleet.Ships : scenario.Defender.Fleet.Ships;
        var shipById = scenarioShips.ToDictionary(ship => ship.ID, ship => ship);

        var initialCountsThisRun = scenarioShips
            .GroupBy(ship => ship.Design.ID)
            .ToDictionary(group => group.Key, group => group.Count(), StringComparer.Ordinal);

        var lossesThisRun = result.ShipReports
            .Where(report => report.Side == side && report.IsSunk)
            .GroupBy(report => ResolveShipType(report.ShipID, shipById))
            .ToDictionary(group => group.Key, group => group.Count(), StringComparer.Ordinal);

        foreach (var kvp in initialCountsThisRun)
        {
            allDesignIds.Add(kvp.Key);

            if (!initialCountsByDesign.ContainsKey(kvp.Key))
            {
                initialCountsByDesign[kvp.Key] = kvp.Value;
            }
        }

        foreach (var designId in allDesignIds)
        {
            var lossCount = lossesThisRun.TryGetValue(designId, out var value) ? value : 0;
            totalLossesByDesign[designId] = totalLossesByDesign.TryGetValue(designId, out var current)
                ? current + lossCount
                : lossCount;
        }
    }

    if (allDesignIds.Count == 0)
    {
        return ["- none"];
    }

    var runCount = iterationResults.Count;
    var output = new List<string>();

    foreach (var designId in allDesignIds)
    {
        var initialCount = initialCountsByDesign.TryGetValue(designId, out var count) ? count : 0;
        var avgLost = totalLossesByDesign.TryGetValue(designId, out var losses) ? losses / runCount : 0;
        var avgLostPercent = initialCount <= 0 ? 0 : avgLost / initialCount;

        output.Add($"- {designId}: {avgLost:F2} lost/run ({avgLostPercent:P1} of {initialCount})");
    }

    return output;
}

