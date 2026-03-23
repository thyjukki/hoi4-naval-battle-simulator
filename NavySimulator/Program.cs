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
Console.WriteLine($"External Naval Strike Planes: {scenario.Attacker.ExternalNavalStrikePlanes}");
Console.WriteLine();

Console.WriteLine("Defender Setup");
Console.WriteLine($"Fleet: {scenario.Defender.Fleet.ID}");
Console.WriteLine($"Ships: {scenario.Defender.Fleet.Ships.Count}");
Console.WriteLine($"Commander: {scenario.Defender.Commander}");
Console.WriteLine($"Doctrine: {scenario.Defender.Doctrine}");
Console.WriteLine($"External Naval Strike Planes: {scenario.Defender.ExternalNavalStrikePlanes}");

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

    File.WriteAllLines(hourlyLogFilePath, result.HourlyLog);
    var shipReportOutput = WriteShipReports(runScenario, result, outputDirectoryPath, fileSuffix);

    var topDamageDealers = result.ShipReports
        .OrderByDescending(report => report.TotalDamageDone)
        .ThenBy(report => report.ShipID)
        .Take(3)
        .Select(report => $"{report.ShipID}:{report.TotalDamageDone:F1}");

    var attackerShipsWithDamage = result.ShipReports.Count(report => report.Side == "Attacker" && report.TotalDamageDone > 0);
    var defenderShipsWithDamage = result.ShipReports.Count(report => report.Side == "Defender" && report.TotalDamageDone > 0);
    var damageBreakdownLines = BuildDamageBreakdownLines(runScenario, result);

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
    Console.WriteLine($"Attacker ship-report folder: {shipReportOutput.AttackerFolder}");
    Console.WriteLine($"Defender ship-report folder: {shipReportOutput.DefenderFolder}");
    Console.WriteLine($"Attacker design reports: {shipReportOutput.AttackerDesignReports}");
    Console.WriteLine($"Defender design reports: {shipReportOutput.DefenderDesignReports}");
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

static (string AttackerFolder, string DefenderFolder, int AttackerDesignReports, int DefenderDesignReports) WriteShipReports(
    BattleScenario scenario,
    BattleResult result,
    string outputDirectoryPath,
    string fileSuffix)
{
    var allShipsById = scenario.Attacker.Fleet.Ships
        .Concat(scenario.Defender.Fleet.Ships)
        .ToDictionary(ship => ship.ID, ship => ship, StringComparer.Ordinal);

    var allShots = BuildShotEvents(result.ShipReports);
    var attackerOutput = WriteSideShipReports("Attacker", result.ShipReports, allShots, allShipsById, outputDirectoryPath, fileSuffix);
    var defenderOutput = WriteSideShipReports("Defender", result.ShipReports, allShots, allShipsById, outputDirectoryPath, fileSuffix);

    return (attackerOutput.FolderPath, defenderOutput.FolderPath, attackerOutput.DesignReportCount, defenderOutput.DesignReportCount);
}

static (string FolderPath, int DesignReportCount) WriteSideShipReports(
    string side,
    List<ShipBattleReport> shipReports,
    List<(string ShooterSide, string ShooterShipID, ShipDamageReportEntry Event)> allShots,
    IReadOnlyDictionary<string, Ship> shipById,
    string outputDirectoryPath,
    string fileSuffix)
{
    var sideFolderPath = Path.Combine(outputDirectoryPath, side.ToLowerInvariant());
    Directory.CreateDirectory(sideFolderPath);

    var sideShipReports = shipReports
        .Where(report => report.Side == side)
        .OrderBy(report => report.ShipID, StringComparer.Ordinal)
        .ToList();

    var sideShipReportPath = Path.Combine(sideFolderPath, $"ship-report{fileSuffix}.txt");
    File.WriteAllLines(sideShipReportPath, BuildSideShipReportLines(sideShipReports, allShots, shipById));

    var byDesign = sideShipReports
        .GroupBy(report => ResolveShipType(report.ShipID, shipById), StringComparer.Ordinal)
        .OrderBy(group => group.Key, StringComparer.Ordinal)
        .ToList();

    foreach (var designGroup in byDesign)
    {
        var fileName = $"ship-design-{SanitizePathSegment(designGroup.Key)}{fileSuffix}.txt";
        var designFilePath = Path.Combine(sideFolderPath, fileName);
        File.WriteAllLines(designFilePath, BuildDesignShipReportLines(side, designGroup.Key, designGroup.ToList(), allShots, shipById));
    }

    return (sideFolderPath, byDesign.Count);
}

static List<(string ShooterSide, string ShooterShipID, ShipDamageReportEntry Event)> BuildShotEvents(List<ShipBattleReport> shipReports)
{
    return shipReports
        .SelectMany(report => report.DamagedShips.Select(damageEvent => (report.Side, report.ShipID, damageEvent)))
        .ToList();
}

static List<string> BuildSideShipReportLines(
    List<ShipBattleReport> shipReports,
    List<(string ShooterSide, string ShooterShipID, ShipDamageReportEntry Event)> allShots,
    IReadOnlyDictionary<string, Ship> shipById)
{
    var lines = new List<string>();

    foreach (var shipReport in shipReports)
    {
        AppendShipDetailLines(lines, shipReport, allShots, shipById);
        lines.Add(string.Empty);
    }

    return lines;
}

static List<string> BuildDesignShipReportLines(
    string side,
    string shipDesignId,
    List<ShipBattleReport> designShipReports,
    List<(string ShooterSide, string ShooterShipID, ShipDamageReportEntry Event)> allShots,
    IReadOnlyDictionary<string, Ship> shipById)
{
    var lines = new List<string>();
    var designShipIds = designShipReports.Select(report => report.ShipID).ToHashSet(StringComparer.Ordinal);
    var designShots = allShots
        .Where(shot => shot.ShooterSide == side && designShipIds.Contains(shot.ShooterShipID))
        .ToList();

    var sunkCount = designShipReports.Count(report => report.IsSunk);
    var totalDamage = designShots.Sum(shot => shot.Event.AppliedHpDamage);
    var sampleAttackStats = BuildDesignAttackStatsLine(designShipReports, shipById);

    lines.Add($"Design: {shipDesignId} ({side})");
    lines.Add($"Ships: {designShipReports.Count}");
    lines.Add($"Sunk: {sunkCount}");
    lines.Add(sampleAttackStats);
    lines.Add($"Total HP Damage Dealt: {totalDamage:F1}");
    lines.Add(string.Empty);
    lines.Add("Summary");
    lines.AddRange(BuildHitMissSummaryLines(designShots));
    lines.AddRange(BuildWeaponTargetingSummaryLines(designShots, shipById));
    lines.AddRange(BuildEnemyDamageShareSummaryLines(designShots, shipById, totalDamage));
    lines.Add(string.Empty);
    lines.Add("Ships");

    foreach (var shipReport in designShipReports.OrderBy(report => report.ShipID, StringComparer.Ordinal))
    {
        AppendShipDetailLines(lines, shipReport, allShots, shipById, "  ");
        lines.Add(string.Empty);
    }

    return lines;
}

static List<string> BuildHitMissSummaryLines(List<(string ShooterSide, string ShooterShipID, ShipDamageReportEntry Event)> designShots)
{
    var lines = new List<string> { "- Hit/Miss ratio per weapon" };
    var weapons = new[] { WeaponType.Light, WeaponType.Heavy, WeaponType.Torpedo, WeaponType.DepthCharge };

    foreach (var weapon in weapons)
    {
        var weaponShots = designShots.Where(shot => shot.Event.Weapon == weapon).ToList();

        if (weaponShots.Count == 0)
        {
            lines.Add($"  - {weapon}: no shots");
            continue;
        }

        var hitCount = weaponShots.Count(shot => shot.Event.DidHit);
        var missCount = weaponShots.Count - hitCount;
        var hitRate = hitCount / (double)weaponShots.Count;
        lines.Add($"  - {weapon}: hits {hitCount}, misses {missCount}, hit rate {hitRate:P1}");
    }

    return lines;
}

static List<string> BuildWeaponTargetingSummaryLines(
    List<(string ShooterSide, string ShooterShipID, ShipDamageReportEntry Event)> designShots,
    IReadOnlyDictionary<string, Ship> shipById)
{
    var lines = new List<string> { "- Weapon targeting % by enemy ship type" };
    var weapons = new[] { WeaponType.Light, WeaponType.Heavy, WeaponType.Torpedo, WeaponType.DepthCharge };

    foreach (var weapon in weapons)
    {
        var weaponShots = designShots.Where(shot => shot.Event.Weapon == weapon).ToList();

        if (weaponShots.Count == 0)
        {
            lines.Add($"  - {weapon}: none");
            continue;
        }

        lines.Add($"  - {weapon}");
        var targetGroups = weaponShots
            .GroupBy(shot => ResolveShipType(shot.Event.TargetShipID, shipById), StringComparer.Ordinal)
            .Select(group => new { TargetType = group.Key, Count = group.Count() })
            .OrderByDescending(group => group.Count)
            .ThenBy(group => group.TargetType, StringComparer.Ordinal)
            .ToList();

        foreach (var targetGroup in targetGroups)
        {
            var share = targetGroup.Count / (double)weaponShots.Count;
            lines.Add($"    - {targetGroup.TargetType}: {targetGroup.Count} ({share:P1})");
        }
    }

    return lines;
}

static List<string> BuildEnemyDamageShareSummaryLines(
    List<(string ShooterSide, string ShooterShipID, ShipDamageReportEntry Event)> designShots,
    IReadOnlyDictionary<string, Ship> shipById,
    double totalDamage)
{
    var lines = new List<string> { "- Outgoing HP damage % by this design's weapon and enemy ship type" };
    var damagingShots = designShots.Where(shot => shot.Event.AppliedHpDamage > 0).ToList();

    if (damagingShots.Count == 0 || totalDamage <= 0)
    {
        lines.Add("  - none");
        return lines;
    }

    var groups = damagingShots
        .GroupBy(
            shot => (Weapon: shot.Event.Weapon, TargetType: ResolveShipType(shot.Event.TargetShipID, shipById)))
        .Select(group => new
        {
            group.Key.Weapon,
            group.Key.TargetType,
            Damage = group.Sum(entry => entry.Event.AppliedHpDamage)
        })
        .OrderByDescending(item => item.Damage)
        .ThenBy(item => item.Weapon)
        .ThenBy(item => item.TargetType, StringComparer.Ordinal)
        .ToList();

    foreach (var item in groups)
    {
        var share = item.Damage / totalDamage;
        lines.Add($"  - Shooter weapon {item.Weapon} -> Enemy type {item.TargetType}: {item.Damage:F1} ({share:P1})");
    }

    return lines;
}

static string BuildDesignAttackStatsLine(
    List<ShipBattleReport> designShipReports,
    IReadOnlyDictionary<string, Ship> shipById)
{
    if (designShipReports.Count == 0)
    {
        return "Per-ship attack stats (LA/HA/Torp/Depth): n/a";
    }

    var firstShipId = designShipReports[0].ShipID;

    if (!shipById.TryGetValue(firstShipId, out var ship))
    {
        return "Per-ship attack stats (LA/HA/Torp/Depth): unknown";
    }

    var stats = ship.GetFinalStats();
    return $"Per-ship attack stats (LA/HA/Torp/Depth): {stats.LightAttack:F1}/{stats.HeavyAttack:F1}/{stats.TorpedoAttack:F1}/{stats.DepthChargeAttack:F1}";
}

static void AppendShipDetailLines(
    List<string> lines,
    ShipBattleReport shipReport,
    List<(string ShooterSide, string ShooterShipID, ShipDamageReportEntry Event)> allShots,
    IReadOnlyDictionary<string, Ship> shipById,
    string indent = "")
{
    var outgoingShots = allShots
        .Where(shot => shot.ShooterShipID == shipReport.ShipID)
        .OrderBy(shot => shot.Event.HourTick)
        .ThenBy(shot => shot.Event.TargetShipID, StringComparer.Ordinal)
        .ToList();
    var outgoingHitShots = outgoingShots
        .Where(shot => shot.Event.DidHit)
        .ToList();
    var incomingDamageEvents = allShots
        .Where(shot => shot.Event.TargetShipID == shipReport.ShipID && shot.Event.AppliedHpDamage > 0)
        .OrderBy(shot => shot.Event.HourTick)
        .ThenBy(shot => shot.ShooterShipID, StringComparer.Ordinal)
        .ToList();

    lines.Add($"{indent}Ship: {shipReport.ShipID} ({shipReport.Side})");
    lines.Add($"{indent}  Design: {ResolveShipType(shipReport.ShipID, shipById)}");
    lines.Add($"{indent}  Sunk: {shipReport.IsSunk}");
    lines.Add($"{indent}  HP: {shipReport.CurrentHp:F1}/{shipReport.MaxHp:F1} ({shipReport.HpPercentage:P1})");
    lines.Add($"{indent}  Retreated: {shipReport.DidRetreat}");
    lines.Add($"{indent}  Attempted Retreat: {shipReport.AttemptedRetreat}");
    lines.Add($"{indent}  Attempted Retreat But Sunk: {shipReport.AttemptedRetreatButSunk}");
    lines.Add($"{indent}  Production Cost: {shipReport.ProductionCost:F1}");
    lines.Add($"{indent}  Total Damage Done: {shipReport.TotalDamageDone:F1}");

    if (shipById.TryGetValue(shipReport.ShipID, out var ship) && ship.Design.Hull.Role == ShipRole.Carrier)
    {
        lines.Add($"{indent}  Carrier Plane Sorties: {shipReport.CarrierPlaneSorties}");

        if (shipReport.CarrierSortiesByHour.Count == 0)
        {
            lines.Add($"{indent}  Carrier Sorties By Hour: none");
        }
        else
        {
            lines.Add($"{indent}  Carrier Sorties By Hour:");

            foreach (var sortieEntry in shipReport.CarrierSortiesByHour)
            {
                lines.Add(
                    $"{indent}    - Hour {sortieEntry.HourTick}, Sorties {sortieEntry.SortiePlanes}, Planes Lost {sortieEntry.PlanesLost}, " +
                    $"Selected Targets {sortieEntry.SelectedTargets}, " +
                    $"Target AA Defense {sortieEntry.TargetAntiAirDefense:F2}, " +
                    $"Combined Fleet AA Damage Reduction {sortieEntry.CombinedFleetAaDamageReduction:P1}, " +
                    $"Final Damage Dealt {sortieEntry.FinalDamageDealt:F1}");
            }
        }
    }

    if (outgoingHitShots.Count == 0)
    {
        lines.Add($"{indent}  Damaged Ships: none");
    }
    else
    {
        lines.Add($"{indent}  Damaged Ships:");

        foreach (var shot in outgoingHitShots)
        {
            var damageEvent = shot.Event;
            var targetType = ResolveShipType(damageEvent.TargetShipID, shipById);
            lines.Add(
                $"{indent}    - Hour {damageEvent.HourTick}, Target {damageEvent.TargetShipID} ({targetType}), Weapon {damageEvent.Weapon}, Hit {damageEvent.DidHit}, Damage {damageEvent.Damage:F1}, " +
                $"HP Damage {damageEvent.AppliedHpDamage:F1}, Org Damage {damageEvent.AppliedOrganizationDamage:F1}, Killing Blow {damageEvent.DidKillingBlow}, " +
                $"Attacker Piercing {damageEvent.AttackerPiercing:F2}, Hit Chance {damageEvent.AttackerFinalHitChance:P1}, " +
                $"Defender Armor {damageEvent.DefenderArmor:F2}, Speed {damageEvent.DefenderSpeed:F2}, Visibility {damageEvent.DefenderVisibility:F2}");
        }
    }

    if (incomingDamageEvents.Count == 0)
    {
        lines.Add($"{indent}  Damage Received: none");
    }
    else
    {
        lines.Add($"{indent}  Damage Received:");

        foreach (var shot in incomingDamageEvents)
        {
            var damageEvent = shot.Event;
            var shooterType = ResolveShipType(shot.ShooterShipID, shipById);
            lines.Add(
                $"{indent}    - Hour {damageEvent.HourTick}, From {shot.ShooterShipID} ({shooterType}), Weapon {damageEvent.Weapon}, Damage {damageEvent.Damage:F1}, " +
                $"HP Damage {damageEvent.AppliedHpDamage:F1}, Org Damage {damageEvent.AppliedOrganizationDamage:F1}, Killing Blow {damageEvent.DidKillingBlow}");
        }
    }
}


static List<string> BuildDamageBreakdownLines(BattleScenario scenario, BattleResult result)
{
    var shipReports = result.ShipReports;
    var shipById = scenario.Attacker.Fleet.Ships
        .Concat(scenario.Defender.Fleet.Ships)
        .ToDictionary(ship => ship.ID, ship => ship);

    var attackerEnemyHealth = scenario.Defender.Fleet.Ships.Sum(ship => ship.GetFinalStats().Hp);
    var defenderEnemyHealth = scenario.Attacker.Fleet.Ships.Sum(ship => ship.GetFinalStats().Hp);

    var lines = new List<string>();
    lines.AddRange(BuildSideDamageBreakdown(
        "Attacker",
        "Attacker",
        attackerEnemyHealth,
        shipReports,
        shipById,
        result.AttackerCarrierPlaneDamage,
        result.AttackerPlaneDamageByType));
    lines.Add(string.Empty);
    lines.AddRange(BuildSideDamageBreakdown(
        "Defender",
        "Defender",
        defenderEnemyHealth,
        shipReports,
        shipById,
        result.DefenderCarrierPlaneDamage,
        result.DefenderPlaneDamageByType));
    return lines;
}

static List<string> BuildSideDamageBreakdown(
    string sideTitle,
    string sideKey,
    double enemyHealth,
    List<ShipBattleReport> shipReports,
    IReadOnlyDictionary<string, Ship> shipById,
    double carrierPlaneDamage,
    IReadOnlyDictionary<string, double> planeDamageByType)
{
    var damageEvents = shipReports
        .Where(report => report.Side == sideKey)
        .SelectMany(report => report.DamagedShips.Select(evt => (ShooterID: report.ShipID, Event: evt)))
        .Where(entry => entry.Event.AppliedHpDamage > 0)
        .ToList();

    var totalDamage = damageEvents.Sum(entry => entry.Event.AppliedHpDamage) + carrierPlaneDamage;

    var lines = new List<string>
    {
        $"{sideTitle} damage summary",
        $"Total damage dealt {totalDamage:F1} ({FormatAsShareOfEnemyHealth(totalDamage, enemyHealth)} of enemy health)",
        "Damage dealt by types"
    };

    lines.Add("- By GUNTYPE");
    var gunTypeExtras = planeDamageByType.ToDictionary(
        kvp => $"Plane:{kvp.Key}",
        kvp => kvp.Value,
        StringComparer.Ordinal);
    lines.AddRange(BuildBreakdownEntriesWithExtra(
        damageEvents,
        enemyHealth,
        entry => entry.Event.Weapon.ToString(),
        gunTypeExtras));

    lines.Add("- By SHIPGROUP");
    var groupExtras = carrierPlaneDamage > 0
        ? new Dictionary<string, double>(StringComparer.Ordinal) { ["Carrier line"] = carrierPlaneDamage }
        : new Dictionary<string, double>(StringComparer.Ordinal);
    lines.AddRange(BuildBreakdownEntriesWithExtra(
        damageEvents,
        enemyHealth,
        entry => ResolveShipGroup(entry.ShooterID, shipById),
        groupExtras));

    lines.Add("- By SHIPTYPE");
    var typeExtras = carrierPlaneDamage > 0
        ? new Dictionary<string, double>(StringComparer.Ordinal) { ["Carrier air wing"] = carrierPlaneDamage }
        : new Dictionary<string, double>(StringComparer.Ordinal);
    lines.AddRange(BuildBreakdownEntriesWithExtra(
        damageEvents,
        enemyHealth,
        entry => ResolveShipType(entry.ShooterID, shipById),
        typeExtras));

    return lines;
}

static List<string> BuildBreakdownEntriesWithExtra(
    List<(string ShooterID, ShipDamageReportEntry Event)> damageEvents,
    double enemyHealth,
    Func<(string ShooterID, ShipDamageReportEntry Event), string> keySelector,
    IReadOnlyDictionary<string, double> extraEntries)
{
    var grouped = damageEvents
        .GroupBy(keySelector)
        .ToDictionary(
            group => group.Key,
            group => group.Sum(entry => entry.Event.AppliedHpDamage),
            StringComparer.Ordinal);

    foreach (var extra in extraEntries)
    {
        grouped[extra.Key] = grouped.TryGetValue(extra.Key, out var current)
            ? current + extra.Value
            : extra.Value;
    }

    var ordered = grouped
        .Select(item => new { Name = item.Key, Damage = item.Value })
        .OrderByDescending(item => item.Damage)
        .ThenBy(item => item.Name, StringComparer.Ordinal)
        .ToList();

    if (ordered.Count == 0)
    {
        return ["  - none"];
    }

    return ordered
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
        $"Depth {totalStats.DepthChargeAttack:F1}, AA {totalStats.AntiAir*Hoi4Defines.SHIP_TO_FLEET_ANTI_AIR_RATIO:F1}");

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

