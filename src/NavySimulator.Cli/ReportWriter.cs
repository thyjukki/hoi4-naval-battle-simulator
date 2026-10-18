using System.Globalization;
using System.Text;
using NavySimulator.Engine;
using NavySimulator.Engine.Battles;
using NavySimulator.Engine.Results;

namespace NavySimulator.Cli;

public static class ReportWriter
{
    private static readonly CultureInfo Invariant = CultureInfo.InvariantCulture;

    public static void WriteRun(string directory, BattleResult result)
    {
        Directory.CreateDirectory(directory);
        File.WriteAllLines(Path.Combine(directory, "summary.txt"), BuildRunSummary(result));
        File.WriteAllLines(Path.Combine(directory, "hourly.log"), result.HourlyLog);
        File.WriteAllText(Path.Combine(directory, "shots.csv"), BuildShotsCsv(result));
        File.WriteAllText(Path.Combine(directory, "ships.csv"), BuildShipsCsv(result));

        if (result.AirStrikes.Count > 0)
        {
            File.WriteAllText(Path.Combine(directory, "air.csv"), BuildAirCsv(result));
        }
    }

    public static List<string> BuildRunSummary(BattleResult result)
    {
        var lines = new List<string>
        {
            $"Scenario {result.ScenarioId}, seed {result.Seed}",
            $"Outcome: {result.Outcome} after {result.HoursFought} hours" +
            (result.Reengagements > 0 ? $" ({result.Reengagements} re-engagements)" : string.Empty),
            string.Empty
        };

        foreach (var side in new[] { result.Attacker, result.Defender })
        {
            lines.Add($"{side.Label} ({side.FleetId}): {side.ShipsAtStart} ships, sunk {side.ShipsSunk}, retreated {side.ShipsRetreated}, remaining {side.ShipsRemaining}");
            lines.Add($"  HP lost {side.HpLost:F1} / {side.HpAtStart:F1} ({Ratio(side.HpLost, side.HpAtStart):P1}), HP damage dealt {side.HpDamageDealt:F1}");
            lines.AddRange(BuildWeaponLines(result, side.Label).Select(line => "  " + line));
            lines.AddRange(BuildAirLines(result, side.Label).Select(line => "  " + line));
            lines.AddRange(BuildDesignLines(result.Ships.Where(ship => ship.Side == side.Label)).Select(line => "  " + line));
        }

        if (result.SkipCounts.Count > 0)
        {
            lines.Add(string.Empty);
            lines.Add("Skipped weapon actions: " + string.Join(", ", result.SkipCounts.OrderBy(entry => entry.Key).Select(entry => $"{entry.Key} {entry.Value}")));
        }

        if (result.Notes.Count > 0)
        {
            lines.Add(string.Empty);
            lines.AddRange(result.Notes.Select(note => "Note: " + note));
        }

        return lines;
    }

    public static List<string> BuildAggregateSummary(BattleScenario scenario, IReadOnlyList<BattleResult> results)
    {
        var lines = new List<string>
        {
            $"Scenario {scenario.ID}: {results.Count} runs, seeds {results.Min(result => result.Seed)}..{results.Max(result => result.Seed)}",
            $"Rules: {scenario.Rules}",
            $"Defines: {string.Join(" -> ", scenario.Defines.Sources)}",
            string.Empty,
            "Outcomes: " + string.Join(", ", results.GroupBy(result => result.Outcome).OrderBy(group => group.Key)
                .Select(group => $"{group.Key} {group.Count()} ({group.Count() / (double)results.Count:P0})")),
            $"Hours: {Stats(results.Select(result => (double)result.HoursFought))}",
            string.Empty
        };

        foreach (var label in new[] { "attacker", "defender" })
        {
            var sides = results.Select(result => label == "attacker" ? result.Attacker : result.Defender).ToList();
            lines.Add($"{label} ({sides[0].FleetId}, {sides[0].ShipsAtStart} ships)");
            lines.Add($"  sunk {Stats(sides.Select(side => (double)side.ShipsSunk))}");
            lines.Add($"  retreated {Stats(sides.Select(side => (double)side.ShipsRetreated))}");
            lines.Add($"  HP lost {Stats(sides.Select(side => side.HpLost))}");
            lines.Add($"  HP damage dealt {Stats(sides.Select(side => side.HpDamageDealt))}");

            var designs = results
                .SelectMany(result => result.Ships.Where(ship => ship.Side == label))
                .GroupBy(ship => ship.DesignId)
                .OrderBy(group => group.Key, StringComparer.Ordinal);

            foreach (var design in designs)
            {
                var perRunCount = design.Count() / (double)results.Count;
                lines.Add($"  {design.Key}: {perRunCount:F0} ships, avg sunk {design.Count(ship => ship.Sunk) / (double)results.Count:F2}, " +
                          $"avg HP dmg dealt per ship {design.Average(ship => ship.HpDamageDealt):F1}");
            }
        }

        var notes = results.SelectMany(result => result.Notes).Distinct().ToList();

        if (notes.Count > 0)
        {
            lines.Add(string.Empty);
            lines.AddRange(notes.Select(note => "Note: " + note));
        }

        return lines;
    }

    private static IEnumerable<string> BuildWeaponLines(BattleResult result, string side)
    {
        return result.Shots
            .Where(shot => shot.ShooterSide == side)
            .GroupBy(shot => shot.Weapon)
            .OrderBy(group => group.Key)
            .Select(group =>
                $"{group.Key}: {group.Count()} shots, {group.Count(shot => shot.Hit)} hits ({Ratio(group.Count(shot => shot.Hit), group.Count()):P1}, " +
                $"avg chance {group.Average(shot => shot.HitChance):P1}), crits {group.Count(shot => shot.Critical)}, HP damage {group.Sum(shot => shot.HpDamage):F1}");
    }

    private static IEnumerable<string> BuildAirLines(BattleResult result, string side)
    {
        var strikes = result.AirStrikes.Where(strike => strike.Side == side).ToList();

        if (strikes.Count == 0)
        {
            return [];
        }

        return
        [
            $"air: {strikes.Count} strikes, {strikes.Sum(strike => strike.Flying)} planes flown, {strikes.Sum(strike => strike.Intercepted)} intercepted, " +
            $"{strikes.Sum(strike => strike.ShotDownByAntiAir)} shot down by AA, {strikes.Sum(strike => strike.Hits)} hits, " +
            $"crits {strikes.Count(strike => strike.Critical)}, HP damage {strikes.Sum(strike => strike.HpDamage):F1}"
        ];
    }

    private static string BuildAirCsv(BattleResult result)
    {
        var builder = new StringBuilder();
        builder.AppendLine("hour,side,source,target,flying,intercepted,shotDownByAa,hits,aaReduction,critical,hpDamage,orgDamage,killingBlow");

        foreach (var strike in result.AirStrikes)
        {
            builder.AppendLine(string.Join(',',
                strike.Hour.ToString(Invariant),
                strike.Side,
                strike.Source,
                strike.TargetId,
                strike.Flying.ToString(Invariant),
                strike.Intercepted.ToString(Invariant),
                strike.ShotDownByAntiAir.ToString(Invariant),
                strike.Hits.ToString(Invariant),
                strike.AntiAirReduction.ToString("F4", Invariant),
                strike.Critical,
                strike.HpDamage.ToString("F3", Invariant),
                strike.OrganizationDamage.ToString("F3", Invariant),
                strike.KillingBlow));
        }

        return builder.ToString();
    }

    private static IEnumerable<string> BuildDesignLines(IEnumerable<ShipResult> ships)
    {
        return ships
            .GroupBy(ship => ship.DesignId)
            .OrderBy(group => group.Key, StringComparer.Ordinal)
            .Select(group =>
                $"{group.Key} x{group.Count()}: sunk {group.Count(ship => ship.Sunk)}, retreated {group.Count(ship => ship.FinalStatus == ShipStatus.Retreated && !ship.Sunk)}, " +
                $"HP dmg dealt {group.Sum(ship => ship.HpDamageDealt):F1}, taken {group.Sum(ship => ship.HpDamageTaken):F1}");
    }

    private static string BuildShotsCsv(BattleResult result)
    {
        var builder = new StringBuilder();
        builder.AppendLine("hour,side,shooter,weapon,target,targetGroup,hitChance,profileFactor,hitModifier,hit,damage,piercingTier,critical,partHit,hpDamage,orgDamage,killingBlow");

        foreach (var shot in result.Shots)
        {
            builder.AppendLine(string.Join(',',
                shot.Hour.ToString(Invariant),
                shot.ShooterSide,
                shot.ShooterId,
                shot.Weapon,
                shot.TargetId,
                shot.TargetGroup,
                shot.HitChance.ToString("F4", Invariant),
                shot.ProfileFactor.ToString("F4", Invariant),
                shot.HitModifier.ToString("F4", Invariant),
                shot.Hit,
                shot.Damage.ToString("F3", Invariant),
                shot.PiercingTier.ToString(Invariant),
                shot.Critical,
                shot.PartHit,
                shot.HpDamage.ToString("F3", Invariant),
                shot.OrganizationDamage.ToString("F3", Invariant),
                shot.KillingBlow));
        }

        return builder.ToString();
    }

    private static string BuildShipsCsv(BattleResult result)
    {
        var builder = new StringBuilder();
        builder.AppendLine("side,ship,design,role,maxHp,finalHp,maxOrg,finalOrg,manpower,startXp,finalXp,status,sunk,sunkHour,retreatedHour,shots,hits,damageDealt,hpDamageDealt,hpDamageTaken");

        foreach (var ship in result.Ships)
        {
            builder.AppendLine(string.Join(',',
                ship.Side,
                ship.ShipId,
                ship.DesignId,
                ship.Role,
                ship.MaxHp.ToString("F2", Invariant),
                ship.FinalHp.ToString("F2", Invariant),
                ship.MaxOrganization.ToString("F2", Invariant),
                ship.FinalOrganization.ToString("F2", Invariant),
                ship.FinalManpower.ToString(Invariant),
                ship.StartExperience.ToString("F2", Invariant),
                ship.FinalExperience.ToString("F2", Invariant),
                ship.FinalStatus,
                ship.Sunk,
                ship.SunkHour?.ToString(Invariant) ?? string.Empty,
                ship.RetreatedHour?.ToString(Invariant) ?? string.Empty,
                ship.ShotsFired.ToString(Invariant),
                ship.Hits.ToString(Invariant),
                ship.DamageDealt.ToString("F2", Invariant),
                ship.HpDamageDealt.ToString("F2", Invariant),
                ship.HpDamageTaken.ToString("F2", Invariant)));
        }

        return builder.ToString();
    }

    private static string Stats(IEnumerable<double> values)
    {
        var list = values.OrderBy(value => value).ToList();
        var mean = list.Average();
        var median = list.Count % 2 == 1 ? list[list.Count / 2] : (list[list.Count / 2 - 1] + list[list.Count / 2]) / 2;
        return $"avg {mean:F2}, median {median:F2}, min {list[0]:F2}, max {list[^1]:F2}";
    }

    private static double Ratio(double part, double whole)
    {
        return whole <= 0 ? 0 : part / whole;
    }
}
