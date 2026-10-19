using NavySimulator.Engine.Random;
using NavySimulator.Engine.Results;

namespace NavySimulator.Engine.Battles.Replay;

/// <summary>
/// S-REPLAY: replays a logged game battle through <see cref="BattleSession"/> hour by hour. Each hour every ship is set
/// to the game's start-of-hour state, the hour is fought with the game's own rolls (<see cref="ReplayRandomSource"/>),
/// and the engine's shots and end-of-hour states are compared with the game's. Setting the state every hour keeps
/// fixed-point rounding from accumulating, so each hour tests the rules on their own.
/// The scenario's sides and fleet order must match the game's (attacker first, ships in the game's member order).
/// </summary>
public static class BattleReplay
{
    /// <summary>
    /// Logged rolls the engine does not make: the bookkeeping rolls of a sinking, and part picks when the data has no
    /// critical parts (the engine then makes none).
    /// </summary>
    public static IReadOnlySet<RollKind> SkippedKinds { get; } = new HashSet<RollKind> { RollKind.Sinking };

    private static readonly IReadOnlySet<RollKind> SkippedWithoutParts = new HashSet<RollKind> { RollKind.Sinking, RollKind.PartPick };

    private const double DamageTolerance = 0.01;

    // The game's hit chance is fixed point (1e-5 of a percent point); allow its truncations.
    private const double HitChanceTolerance = 2e-5;
    private const double StateTolerance = 0.02;
    private const double FractionTolerance = 1e-4;

    /// <summary>
    /// Replays the hours; <paramref name="onHour"/> gets each hour's game and engine shots, position by position, for
    /// diagnostics beyond the first mismatch.
    /// </summary>
    public static IReadOnlyList<ReplayHourReport> Run(BattleScenario scenario, IReadOnlyList<ReplayHour> hours,
        Action<int, IReadOnlyList<ReplayShot>, IReadOnlyList<ShotRecord>>? onHour = null)
    {
        var hasParts = scenario.Attacker.Fleet.Ships.Concat(scenario.Defender.Fleet.Ships).Any(ship => ship.Design.CriticalPartCandidates.Count > 0);
        var random = new ReplayRandomSource(hasParts ? SkippedKinds : SkippedWithoutParts);
        var session = new BattleSession(scenario, random);
        var shipsById = session.Ships.ToDictionary(ship => ship.ID, StringComparer.Ordinal);
        var reports = new List<ReplayHourReport>();

        foreach (var hour in hours.OrderBy(entry => entry.Hour))
        {
            if (session.IsOver || hour.Hour != session.HoursFought)
            {
                break;
            }

            foreach (var (id, state) in hour.Start)
            {
                if (shipsById.TryGetValue(id, out var ship))
                {
                    Apply(ship, state);
                }
            }

            var shotsBefore = session.Shots.Count;
            random.Load(hour.Rolls);
            session.StepHour();
            random.Finish();

            var engineShots = session.Shots.Skip(shotsBefore).ToList();
            onHour?.Invoke(hour.Hour, hour.Shots, engineShots);
            var (matching, firstMismatch) = CompareShots(hour.Shots, engineShots);
            var gameDamage = hour.Shots.Where(shot => shipsById.ContainsKey(shot.ShooterId))
                .GroupBy(shot => session.SideOf(shipsById[shot.ShooterId]))
                .ToDictionary(group => group.Key, group => group.Sum(shot => shot.HpDamage));
            var engineDamage = engineShots.GroupBy(shot => shot.ShooterSide).ToDictionary(group => group.Key, group => group.Sum(shot => shot.HpDamage));
            reports.Add(new ReplayHourReport(hour.Hour, random.Divergence, random.Skipped, hour.Shots.Count, engineShots.Count,
                matching, firstMismatch, CompareStates(hour.End, shipsById), gameDamage, engineDamage));
        }

        return reports;
    }

    private static void Apply(Ship ship, ReplayShipState state)
    {
        ship.CurrentHP = state.Sunk ? 0 : state.Hp;
        ship.CurrentOrganization = state.Organization;
        ship.CurrentManpower = state.Manpower;
        ship.Experience = state.Experience;
        ship.CurrentStatus = state.Status;
        ship.RetreatProgress = state.RetreatProgress;
        ship.AttemptedRetreat = state.Status is ShipStatus.Retreating or ShipStatus.Retreated;

        for (var i = 0; i < ship.SlotCharge.Length && i < state.Charges.Length; i++)
        {
            ship.SlotCharge[i] = state.Charges[i];
        }
    }

    private static (int Matching, ReplayShotMismatch? First) CompareShots(IReadOnlyList<ReplayShot> game, IReadOnlyList<ShotRecord> engine)
    {
        var matching = 0;
        ReplayShotMismatch? first = null;

        for (var i = 0; i < Math.Max(game.Count, engine.Count); i++)
        {
            var g = i < game.Count ? game[i] : null;
            var e = i < engine.Count ? engine[i] : null;
            var difference = g is null ? "the game fired fewer shots"
                : e is null ? "the engine fired fewer shots"
                : ShotDifference(g, e);

            if (difference is null)
            {
                matching++;
            }
            else
            {
                first ??= new ReplayShotMismatch(i, g, e, difference);
            }
        }

        return (matching, first);
    }

    private static string? ShotDifference(ReplayShot game, ShotRecord engine)
    {
        if (game.ShooterId != engine.ShooterId)
        {
            return "shooter";
        }

        if (game.Weapon != engine.Weapon)
        {
            return "weapon";
        }

        if (game.TargetId != engine.TargetId)
        {
            return "target";
        }

        if (game.Hit != engine.Hit)
        {
            return "hit";
        }

        if (!double.IsNaN(game.HitChance) && Math.Abs(game.HitChance - engine.HitChance) > HitChanceTolerance)
        {
            return "hit chance";
        }

        if (Math.Abs(game.HpDamage - engine.HpDamage) > DamageTolerance)
        {
            return "HP damage";
        }

        return Math.Abs(game.OrganizationDamage - engine.OrganizationDamage) > DamageTolerance ? "organization damage" : null;
    }

    private static List<ReplayStateMismatch> CompareStates(
        IReadOnlyDictionary<string, ReplayShipState> end,
        IReadOnlyDictionary<string, Ship> shipsById)
    {
        var mismatches = new List<ReplayStateMismatch>();

        foreach (var (id, game) in end)
        {
            if (!shipsById.TryGetValue(id, out var ship))
            {
                continue;
            }

            void Check(string field, double gameValue, double engineValue, double tolerance)
            {
                if (Math.Abs(gameValue - engineValue) > tolerance)
                {
                    mismatches.Add(new ReplayStateMismatch(id, field, gameValue, engineValue));
                }
            }

            Check("sunk", game.Sunk ? 1 : 0, ship.IsSunk ? 1 : 0, 0);

            if (game.Sunk || ship.IsSunk)
            {
                continue;
            }

            Check("status", (int)game.Status, (int)ship.CurrentStatus, 0);
            Check("HP", game.Hp, ship.CurrentHP, StateTolerance);
            Check("organization", game.Organization, ship.CurrentOrganization, StateTolerance);
            Check("experience", game.Experience, ship.Experience, StateTolerance);
            Check("escape progress", game.RetreatProgress, ship.RetreatProgress, FractionTolerance);

            Check("manpower", game.Manpower, ship.CurrentManpower, 0);

            for (var i = 0; i < ship.SlotCharge.Length && i < game.Charges.Length; i++)
            {
                Check($"charge {(WeaponSlot)i}", game.Charges[i], ship.SlotCharge[i], FractionTolerance);
            }
        }

        return mismatches;
    }
}
