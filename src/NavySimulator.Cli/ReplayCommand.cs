using NavySimulator.Engine.Battles;
using NavySimulator.Engine.Battles.Replay;
using NavySimulator.GameData;
using NavySimulator.GameData.Contracts;
using NavySimulator.GameData.Replay;

namespace NavySimulator.Cli;

/// <summary>
/// <c>NavySimulator replay</c>: replays a battle logged in the game (<c>tools/ingame/run_battles.py</c>) hour by hour
/// with the game's own rolls and reports where the engine's shots and ship states differ (S-REPLAY).
/// </summary>
public static class ReplayCommand
{
    public const string Name = "replay";

    public const string Usage = """
        Usage: NavySimulator replay --log <run folder> [--data <dir>] [--scenario <file>] [--defines <path>]... [--details <n>] [--shots <file>] [--states <file>] [--air <file>] [--region <file>]
                                [--design <game name>=<design id>]... [--constant-roll <U>]

          --log <dir>         A run folder of tools/ingame/run_battles.py (meta.json, raw.jsonl), logged with rolls
          --data <dir>        Data directory (default: ./data)
          --scenario <file>   Scenario the battle was generated from (default: battle-scenario.json)
          --defines <path>    Define source, as for a battle run (the game's mod folder)
          --details <n>       Hours to describe in detail (default 10)
          --shots <file>      Write every shot, game and engine side by side, as CSV
          --states <file>     Write every end-of-hour ship value that differs, as CSV
          --air <file>        Write every air strike, game and engine side by side, as CSV
          --region <file>     Write the region air war check per wing and air hour (engine from the game's start, game), as CSV
          --design <a>=<b>    The logged variant name a is the scenario's design b (a design renamed after the battle was
                              recorded, docs/calibration.md). Repeatable.
          --constant-roll <U> Fight the battle free from the game's first hour with every roll U, for a run logged with
                              run_battles.py --constant-roll U (S-REPLAY-CONSTANT); each hour then compares the whole
                              battle so far. Surface battles only.

        Each hour starts from the game's logged state and is fought with the game's rolls; the attacker is the game's.
        """;

    public static int Run(string[] args)
    {
        var data = Path.Combine(Directory.GetCurrentDirectory(), "data");
        var scenarioFile = "battle-scenario.json";
        var defineSources = new List<string>();
        string? logDirectory = null;
        var details = 10;
        string? shotsFile = null;
        string? statesFile = null;
        string? airFile = null;
        string? regionFile = null;
        var designNames = new Dictionary<string, string>(StringComparer.Ordinal);
        double? constantRoll = null;

        for (var i = 0; i < args.Length; i++)
        {
            if (args[i] is "--help" or "-h")
            {
                Console.WriteLine(Usage);
                return 0;
            }

            if (i + 1 >= args.Length)
            {
                Console.Error.WriteLine($"Missing value for {args[i]}.");
                Console.Error.WriteLine(Usage);
                return 2;
            }

            switch (args[i])
            {
                case "--log":
                    logDirectory = args[++i];
                    break;
                case "--data":
                    data = args[++i];
                    break;
                case "--scenario":
                    scenarioFile = args[++i];
                    break;
                case "--defines":
                    defineSources.Add(args[++i]);
                    break;
                case "--details":
                    details = int.Parse(args[++i]);
                    break;
                case "--shots":
                    shotsFile = args[++i];
                    break;
                case "--states":
                    statesFile = args[++i];
                    break;
                case "--air":
                    airFile = args[++i];
                    break;
                case "--region":
                    regionFile = args[++i];
                    break;
                case "--design":
                    var pair = args[++i].Split('=', 2);

                    if (pair.Length != 2 || pair[0].Length == 0 || pair[1].Length == 0)
                    {
                        Console.Error.WriteLine($"--design expects <game name>=<design id>, got '{args[i]}'.");
                        return 2;
                    }

                    designNames[pair[0]] = pair[1];
                    break;
                case "--constant-roll":
                    constantRoll = double.Parse(args[++i], System.Globalization.CultureInfo.InvariantCulture);

                    if (constantRoll is < 0 or >= 1)
                    {
                        Console.Error.WriteLine($"--constant-roll expects a value in [0, 1), got '{args[i]}'.");
                        return 2;
                    }

                    break;
                default:
                    Console.Error.WriteLine($"Unknown argument '{args[i]}'.");
                    Console.Error.WriteLine(Usage);
                    return 2;
            }
        }

        if (logDirectory is null)
        {
            Console.Error.WriteLine("--log is required.");
            Console.Error.WriteLine(Usage);
            return 2;
        }

        GameBattleLog log;
        BattleScenario scenario;

        try
        {
            log = GameBattleLog.Read(logDirectory, designNames);
            var loader = new SetupLoader();
            var catalog = loader.ReadCatalog(data);
            var scenarioDto = SetupLoader.ReadScenario(data, scenarioFile);
            scenario = loader.BuildScenario(catalog, scenarioDto, defineSources);

            if (!log.SidesMatch(scenario))
            {
                // The game decides who attacks; build the scenario the other way round.
                (scenarioDto.Attacker, scenarioDto.Defender) = (scenarioDto.Defender, scenarioDto.Attacker);
                scenario = loader.BuildScenario(catalog, scenarioDto, defineSources);
            }
        }
        catch (Exception exception) when (exception is IOException or InvalidDataException or SetupValidationException or UnauthorizedAccessException)
        {
            Console.Error.WriteLine(exception is SetupValidationException validation ? string.Join(Environment.NewLine, validation.Errors) : exception.Message);
            return 1;
        }

        if (!log.SidesMatch(scenario))
        {
            Console.Error.WriteLine($"The logged battle's sides ({log.Country(0)} attacking, {log.Country(1)} defending) do not have the scenario's designs.");
            return 1;
        }

        // A-MIXED-REMOVE: a battle logged in 1.19.3 lost its planes oldest type first.
        scenario = scenario.WithRules(log.RulesFor(scenario.Rules));
        Console.WriteLine($"Logged in game version {log.GameVersion}");
        var map = log.MapOnto(scenario);
        using var shots = shotsFile is null ? null : new StreamWriter(shotsFile);
        shots?.WriteLine("hour,index,gameShooter,gameWeapon,gameTarget,gameHit,gameHitChance,gameHpDamage," +
                         "engineSide,engineShooter,engineWeapon,engineTarget,engineHit,engineHitChance,engineProfileFactor,engineHitModifier,engineDamage,enginePiercingTier,engineHpDamage");
        using var air = airFile is null ? null : new StreamWriter(airFile);
        air?.WriteLine("hour,index,gameCarrier,gameSortie,gameTarget,gameStrikers,gameIntercepted,gameShotDown,gameHpDamage,gameOrgDamage," +
                       "engineSource,engineTarget,engineFlying,engineIntercepted,engineShotDown,engineHits,engineReduction,engineCritical,engineHpDamage,engineOrgDamage");
        Action<int, IReadOnlyList<ReplayAirStrike>, IReadOnlyList<Engine.Results.AirStrikeRecord>>? onAir = air is null ? null : (hour, game, engine) =>
        {
            var invariant = System.Globalization.CultureInfo.InvariantCulture;

            for (var i = 0; i < Math.Max(game.Count, engine.Count); i++)
            {
                var g = i < game.Count ? game[i] : null;
                var e = i < engine.Count ? engine[i] : null;
                air.WriteLine(string.Join(",",
                    hour, i,
                    g?.CarrierId, g?.Sortie?.ToString("R", invariant), g?.TargetId, g?.Strikers, g?.Intercepted, g?.ShotDown,
                    g?.HpDamage.ToString("R", invariant), g?.OrganizationDamage.ToString("R", invariant),
                    e?.Source, e?.TargetId, e?.Flying, e?.Intercepted, e?.ShotDownByAntiAir, e?.Hits, e?.AntiAirReduction.ToString("R", invariant),
                    e?.Critical, e?.HpDamage.ToString("R", invariant), e?.OrganizationDamage.ToString("R", invariant)));
            }
        };
        using var region = regionFile is null ? null : new StreamWriter(regionFile);
        region?.WriteLine("hour,side,source,plane,wing,startPlanes,engineDisruption,gameDisruption,engineLosses,gameLosses");
        Action<IReadOnlyList<ReplayRegionWing>>? onRegion = region is null ? null : wings =>
        {
            var invariant = System.Globalization.CultureInfo.InvariantCulture;

            foreach (var w in wings)
            {
                region.WriteLine(string.Join(",", w.Hour, w.Side, w.Source, w.Plane, w.WingId, w.StartPlanes,
                    w.EngineDisruption.ToString("R", invariant), w.GameDisruption.ToString("R", invariant), w.EngineLosses, w.GameLosses));
            }
        };
        var reports = BattleReplay.Run(scenario, log.Hours(map), shots is null ? null : (hour, game, engine) =>
        {
            var invariant = System.Globalization.CultureInfo.InvariantCulture;

            for (var i = 0; i < Math.Max(game.Count, engine.Count); i++)
            {
                var g = i < game.Count ? game[i] : null;
                var e = i < engine.Count ? engine[i] : null;
                shots.WriteLine(string.Join(",",
                    hour, i,
                    g?.ShooterId, g?.Weapon, g?.TargetId, g?.Hit, g?.HitChance.ToString("R", invariant), g?.HpDamage.ToString("R", invariant),
                    e?.ShooterSide, e?.ShooterId, e?.Weapon, e?.TargetId, e?.Hit, e?.HitChance.ToString("R", invariant),
                    e?.ProfileFactor.ToString("R", invariant), e?.HitModifier.ToString("R", invariant), e?.Damage.ToString("R", invariant),
                    e?.PiercingTier, e?.HpDamage.ToString("R", invariant)));
            }
        }, onAir, onRegion, constantRoll);
        if (statesFile is not null)
        {
            var invariant = System.Globalization.CultureInfo.InvariantCulture;
            File.WriteAllLines(statesFile, reports.SelectMany(report => report.StateMismatches.Select(mismatch => string.Join(",",
                    report.Hour, mismatch.ShipId, mismatch.Field, mismatch.Game.ToString("R", invariant), mismatch.Engine.ToString("R", invariant))))
                .Prepend("hour,ship,field,game,engine"));
        }

        Print(scenario, log, reports, details);
        return 0;
    }

    private static void Print(BattleScenario scenario, GameBattleLog log, IReadOnlyList<ReplayHourReport> reports, int details)
    {
        Console.WriteLine($"Replay of {scenario.ID}: game attacker {log.Country(0)} = {scenario.Attacker.Fleet.ID}, defender {log.Country(1)} = {scenario.Defender.Fleet.ID}");
        Console.WriteLine($"Hours logged {log.HourCount}, replayed {reports.Count}, matching exactly {reports.Count(report => report.Matches)}");
        var over = reports.FirstOrDefault(report => report.EngineOver);
        Console.WriteLine(over is null ? "Engine battle: not over after the replayed hours" : $"Engine battle: over after hour {over.Hour} (the game's last logged hour is {reports[^1].Hour})");
        Console.WriteLine($"Shots: game {reports.Sum(report => report.GameShots)}, engine {reports.Sum(report => report.EngineShots)}, " +
                          $"matching position by position {reports.Sum(report => report.MatchingShots)}; logged rolls skipped {reports.Sum(report => report.SkippedRolls)}");

        foreach (var side in new[] { "attacker", "defender" })
        {
            var game = reports.Sum(report => report.GameHpDamage.GetValueOrDefault(side));
            var engine = reports.Sum(report => report.EngineHpDamage.GetValueOrDefault(side));
            Console.WriteLine($"HP damage dealt by the {side}: game {game:F1}, engine {engine:F1} ({(game > 0 ? engine / game - 1 : 0):+0.00%;-0.00%})");
        }

        var gameAir = reports.Sum(report => report.GameAirHpDamage);
        var engineAir = reports.Sum(report => report.EngineAirHpDamage);
        Console.WriteLine($"Air strikes: game {reports.Sum(report => report.GameAirStrikes)}, engine {reports.Sum(report => report.EngineAirStrikes)}, " +
                          $"matching {reports.Sum(report => report.MatchingAirStrikes)}; air HP damage game {gameAir:F1}, engine {engineAir:F1} " +
                          $"({(gameAir > 0 ? engineAir / gameAir - 1 : 0):+0.00%;-0.00%})");

        var divergences = reports.Where(report => report.Divergence is not null)
            .GroupBy(report => $"engine {report.Divergence!.Requested?.ToString() ?? "done"} / game {Describe(report.Divergence.Logged)}")
            .OrderByDescending(group => group.Count());
        Console.WriteLine("Roll divergences (engine asked for / game rolled):");

        foreach (var group in divergences)
        {
            Console.WriteLine($"  {group.Count(),5} h  {group.Key}");
        }

        Console.WriteLine("End-of-hour differences by field:");

        foreach (var group in reports.SelectMany(report => report.StateMismatches).GroupBy(mismatch => mismatch.Field).OrderByDescending(group => group.Count()))
        {
            Console.WriteLine($"  {group.Count(),6}  {group.Key}");
        }

        Console.WriteLine("Shot differences by kind:");

        foreach (var group in reports.Where(report => report.FirstShotMismatch is not null).GroupBy(report => report.FirstShotMismatch!.Difference).OrderByDescending(group => group.Count()))
        {
            Console.WriteLine($"  {group.Count(),5} h  {group.Key}");
        }

        Console.WriteLine("Air strike differences by kind:");

        foreach (var group in reports.Where(report => report.FirstAirMismatch is not null)
                     .GroupBy(report => report.FirstAirMismatch!.Split(':')[0].Split(" differs in ").Last())
                     .OrderByDescending(group => group.Count()))
        {
            Console.WriteLine($"  {group.Count(),5} h  {group.Key}");
        }

        foreach (var report in reports.Where(report => !report.Matches).Take(details))
        {
            Console.WriteLine();
            Console.WriteLine($"Hour {report.Hour}: shots game {report.GameShots}, engine {report.EngineShots}, matching {report.MatchingShots}");

            if (report.Divergence is { } divergence)
            {
                Console.WriteLine($"  roll {divergence.Position}: engine asked for {divergence.Requested?.ToString() ?? "nothing more"}, game rolled {Describe(divergence.Logged)}");
            }

            if (report.FirstShotMismatch is { } shot)
            {
                Console.WriteLine($"  shot {shot.Index} differs in {shot.Difference}:");
                Console.WriteLine($"    game   {(shot.Game is { } g ? $"{g.ShooterId} {g.Weapon} -> {g.TargetId} hit {g.Hit} HP {g.HpDamage:F3} org {g.OrganizationDamage:F3} crit {g.Critical} part rolled {g.PartRolled} damaged {g.PartDamaged} (hit chance {g.HitChance:F5})" : "-")}");
                Console.WriteLine($"    engine {(shot.Engine is { } e ? $"{e.ShooterId} {e.Weapon} -> {e.TargetId} hit {e.Hit} HP {e.HpDamage:F3} org {e.OrganizationDamage:F3} crit {e.Critical} part {e.PartHit} (hit chance {e.HitChance:F5})" : "-")}");
            }

            if (report.FirstAirMismatch is { } airMismatch)
            {
                Console.WriteLine($"  air: {airMismatch}");
            }

            foreach (var mismatch in report.StateMismatches.Take(5))
            {
                Console.WriteLine($"  {mismatch.ShipId} {mismatch.Field}: game {mismatch.Game:F5}, engine {mismatch.Engine:F5}");
            }

            if (report.StateMismatches.Count > 5)
            {
                Console.WriteLine($"  ... {report.StateMismatches.Count - 5} more state differences");
            }
        }
    }

    private static string Describe(Engine.Random.ReplayRoll? roll)
    {
        return roll is { } r ? $"{r.Kind} (line {r.Line:x}, {r.Value:F5})" : "nothing more";
    }
}
