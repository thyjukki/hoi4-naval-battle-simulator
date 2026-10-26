using NavySimulator.Engine.Battles.Air;
using NavySimulator.Engine.Environment;
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
    public static IReadOnlySet<RollKind> SkippedKinds { get; } = new HashSet<RollKind> { RollKind.Sinking, RollKind.AirBookkeeping };

    private static readonly IReadOnlySet<RollKind> SkippedWithoutParts =
        new HashSet<RollKind> { RollKind.Sinking, RollKind.AirBookkeeping, RollKind.PartPick };

    private const double DamageTolerance = 0.01;

    // The game's hit chance is fixed point (1e-5 of a percent point); allow its truncations.
    private const double HitChanceTolerance = 2e-5;
    private const double StateTolerance = 0.02;
    private const double FractionTolerance = 1e-4;

    /// <summary>
    /// Replays the hours; <paramref name="onHour"/> gets each hour's game and engine shots, position by position, for
    /// diagnostics beyond the first mismatch. With <paramref name="constantRoll"/> (S-REPLAY-CONSTANT) the battle is not
    /// replayed but fought free from the game's first logged hour: every engine roll is that value, as the game's were
    /// when logged with <c>run_battles.py --constant-roll</c>, and no later state is taken from the log, so each hour
    /// compares the whole battle so far. Air wings and side values are set in the first hour only; the region's air war
    /// runs in the engine, and accidents are off on both sides.
    /// </summary>
    public static IReadOnlyList<ReplayHourReport> Run(BattleScenario scenario, IReadOnlyList<ReplayHour> hours,
        Action<int, IReadOnlyList<ReplayShot>, IReadOnlyList<ShotRecord>>? onHour = null,
        Action<int, IReadOnlyList<ReplayAirStrike>, IReadOnlyList<AirStrikeRecord>>? onAirHour = null,
        Action<IReadOnlyList<ReplayRegionWing>>? onRegionHour = null,
        double? constantRoll = null)
    {
        var hasParts = scenario.Attacker.Fleet.Ships.Concat(scenario.Defender.Fleet.Ships).Any(ship => ship.Design.CriticalPartCandidates.Count > 0);
        var replayRandom = constantRoll is null
            ? new ReplayRandomSource(hasParts ? SkippedKinds : SkippedWithoutParts, unlogged: new HashSet<RollKind> { RollKind.AirAceTemplate, RollKind.AirAccident })
            : null;
        var session = new BattleSession(scenario, replayRandom ?? (IRandomSource)new ConstantRandomSource(constantRoll!.Value));
        // The region's air war happens outside the logged combat update: the wings' planes and disruption come from the log.
        // S-REPLAY-CONSTANT: a free battle fights it from its second hour (the logger forces its rolls too; the first
        // hour's state already holds the game's) and, like the logged game, has no accidents.
        session.AirPhase.RegionAirWarEnabled = false;
        session.AirPhase.AccidentsEnabled = constantRoll is null;
        var shipsById = session.Ships.ToDictionary(ship => ship.ID, StringComparer.Ordinal);
        var reports = new List<ReplayHourReport>();
        var wingIds = new Dictionary<AirGroup, List<uint>>();
        var airObjects = new Dictionary<string, AirGroup>(StringComparer.Ordinal);

        foreach (var hour in hours.OrderBy(entry => entry.Hour))
        {
            if (session.IsOver || hour.Hour != session.HoursFought)
            {
                break;
            }

            // S-REPLAY-CONSTANT: a free battle takes the game's state at its start only.
            var setState = constantRoll is null || reports.Count == 0;
            session.AirPhase.RegionAirWarEnabled = !setState;

            foreach (var (id, state) in setState ? hour.Start : new Dictionary<string, ReplayShipState>())
            {
                if (shipsById.TryGetValue(id, out var ship))
                {
                    Apply(ship, state);
                }
            }

            var air = hour.Air ?? [];

            if (onRegionHour is not null && air.Count > 0 && hour.StartWingPlanes is { } startWings)
            {
                onRegionHour(CheckRegion(session, hour, startWings, wingIds));
            }

            if (setState)
            {
                ApplyAir(session, shipsById, air, wingIds, airObjects);
                ApplySides(session, hour.StartSides);
            }

            foreach (var (pointer, planes) in (setState ? hour.StartAirObjectPlanes : null) ?? new Dictionary<string, int>())
            {
                if (airObjects.TryGetValue(pointer, out var group))
                {
                    group.CountedPlanes = planes;
                }
            }

            session.DarknessOverride = hour.Darkness;
            // S-WEATHER: the logged weather type (combat+0xa8); sandstorm and arctic water log as 0 and replay as clear.
            session.WeatherOverride = hour.Weather is { } weatherType ? RegionWeather.FromCombatType(weatherType) : null;
            var shotsBefore = session.Shots.Count;
            var strikesBefore = session.AirStrikes.Count;
            replayRandom?.Load(hour.Rolls);
            session.StepHour();
            replayRandom?.Finish();
            var engineStrikes = session.AirStrikes.Skip(strikesBefore).ToList();
            var gameStrikes = air.Where(strike => strike.Struck).ToList();
            var (airMatching, airMismatch) = CompareAir(gameStrikes, engineStrikes);
            onAirHour?.Invoke(hour.Hour, gameStrikes, engineStrikes);

            var engineShots = session.Shots.Skip(shotsBefore).ToList();
            onHour?.Invoke(hour.Hour, hour.Shots, engineShots);
            var (matching, firstMismatch) = CompareShots(hour.Shots, engineShots);
            var gameDamage = hour.Shots.Where(shot => shipsById.ContainsKey(shot.ShooterId))
                .GroupBy(shot => session.SideOf(shipsById[shot.ShooterId]))
                .ToDictionary(group => group.Key, group => group.Sum(shot => shot.HpDamage));
            var engineDamage = engineShots.GroupBy(shot => shot.ShooterSide).ToDictionary(group => group.Key, group => group.Sum(shot => shot.HpDamage));
            var states = CompareStates(hour.End, shipsById);
            states.AddRange(CompareSides(hour.Sides, session));
            reports.Add(new ReplayHourReport(hour.Hour, replayRandom?.Divergence, replayRandom?.Skipped ?? 0, hour.Shots.Count, engineShots.Count,
                matching, firstMismatch, states, gameDamage, engineDamage,
                gameStrikes.Count, engineStrikes.Count, airMatching, airMismatch,
                gameStrikes.Sum(strike => strike.HpDamage), engineStrikes.Sum(strike => strike.HpDamage), session.IsOver));
        }

        return reports;
    }

    // A-WINGS: each carrier's air objects in the game's order are its wing pools in the engine's order (both by first
    // wing); the game's wing planes at the start of the strike are the hour's start (only its own strike removes planes).
    private static void ApplyAir(BattleSession session, IReadOnlyDictionary<string, Ship> shipsById, IReadOnlyList<ReplayAirStrike> air,
        Dictionary<AirGroup, List<uint>> wingIds, Dictionary<string, AirGroup> airObjects)
    {
        foreach (var byCarrier in air.Where(strike => strike.CarrierId is not null).GroupBy(strike => strike.CarrierId!))
        {
            if (!shipsById.TryGetValue(byCarrier.Key, out var carrier))
            {
                continue;
            }

            var side = session.SideOf(carrier) == session.Attacker.Label ? session.Attacker : session.Defender;
            var groups = session.AirPhase.GroupsOf(side).Where(group => group.Carrier == carrier).ToList();

            // An air object keeps its pointer for the battle; it is matched by order the first hour all of the carrier's
            // pools are logged (a pool emptied by the region's air war has no air object any more).
            if (byCarrier.Count() == groups.Count)
            {
                foreach (var (strike, group) in byCarrier.Zip(groups))
                {
                    if (strike.AirObject is { } pointer)
                    {
                        airObjects.TryAdd(pointer, group);
                    }
                }
            }

            foreach (var strike in byCarrier)
            {
                if (strike.AirObject is { } pointer && airObjects.TryGetValue(pointer, out var group))
                {
                    SetPlanes(group, strike, wingIds, session.Scenario.AceTemplates);
                }
            }
        }

        // A-EXTERNAL: the game's land-based air objects of a side in joining order are the side's land-based entries that
        // join; the side is the one whose enemy the strike targets. Their attack factor (the disruption) is the game's.
        foreach (var side in new[] { session.Attacker, session.Defender })
        {
            var enemyIds = (ReferenceEquals(side, session.Attacker) ? session.Defender : session.Attacker).Ships.Select(ship => ship.ID).ToHashSet();
            var logged = air.Where(strike => strike.External && strike.TargetId is { } target && enemyIds.Contains(target));
            var groups = session.AirPhase.GroupsOf(side).Where(group => group.LandBased is { JoinsNavalBattle: true });

            foreach (var (strike, group) in logged.Zip(groups))
            {
                SetPlanes(group, strike, wingIds, session.Scenario.AceTemplates);
                group.DisruptionFactor = strike.ExternalFactor ?? group.DisruptionFactor;
            }
        }
    }

    // The game's wings by handle: the first time a group is seen its wings are the game's in order; a wing the game has
    // deleted since (the region's air war empties and removes wings) is removed from the engine's group too.
    private static void SetPlanes(AirGroup group, ReplayAirStrike strike, Dictionary<AirGroup, List<uint>> wingIds,
        IReadOnlyList<AceTemplate>? aceTemplates = null)
    {
        if (strike.WingIds is { } ids)
        {
            if (!wingIds.TryGetValue(group, out var known))
            {
                wingIds[group] = known = ids.Take(group.Wings.Count).ToList();
            }

            for (var i = known.Count - 1; i >= 0; i--)
            {
                if (!ids.Contains(known[i]) && i < group.Wings.Count)
                {
                    group.Wings.RemoveAt(i);
                    known.RemoveAt(i);
                }
            }
        }

        for (var i = 0; i < group.Wings.Count && i < strike.WingPlanes.Count; i++)
        {
            group.Wings[i].Planes = strike.WingPlanes[i];
        }

        // A-REGION-DISRUPT: the disruption each wing's mission took in the game's region air war this hour.
        if (strike.WingDisruption is { } disruption)
        {
            for (var i = 0; i < group.Wings.Count && i < disruption.Count; i++)
            {
                group.Wings[i].Disruption = disruption[i];
            }
        }

        // A-ACE: the game's aces, when logged (an ace can move to another wing of the pool when its wing is deleted).
        if (strike.Aces is { } aces && aceTemplates is not null)
        {
            for (var i = 0; i < group.Wings.Count && i < aces.Count; i++)
            {
                var wing = group.Wings[i];
                var logged = aces[i];

                if (logged is null)
                {
                    wing.Ace = null;
                    continue;
                }

                var types = wing.Plane.SubUnitTypes.Count > 0 ? wing.Plane.SubUnitTypes : wing.Plane.Types;
                var template = aceTemplates.FirstOrDefault(candidate => candidate.Types.Overlaps(types) &&
                                                                        Math.Abs(candidate.Chance - (logged.TemplateChance ?? -1)) < 1e-5);

                if (template is not null && wing.Ace?.Template != template)
                {
                    wing.Ace = new AirAce(template, -1);
                }

                if (wing.Ace is { } ace)
                {
                    ace.Alive = logged.Alive;
                }
            }
        }
    }

    // A-REGION check: the engine's region air war as a dry run from the game's wings at the start of the hour, against the
    // game's wings at their strike (disruption after escorts, planes lost since the start of the hour).
    private static List<ReplayRegionWing> CheckRegion(BattleSession session, ReplayHour hour, IReadOnlyDictionary<uint, int> startWings,
        Dictionary<AirGroup, List<uint>> wingIds)
    {
        var result = new List<ReplayRegionWing>();
        var game = new Dictionary<uint, (int Planes, double Disruption)>();

        foreach (var strike in hour.Air ?? [])
        {
            for (var i = 0; strike.WingIds is { } ids && i < ids.Count && i < strike.WingPlanes.Count; i++)
            {
                game[ids[i]] = (strike.WingPlanes[i], strike.WingDisruption is { } d && i < d.Count ? d[i] : 0);
            }
        }

        var wingsById = new Dictionary<uint, (AirWing Wing, AirGroup Group, BattleSide Side)>();

        foreach (var side in new[] { session.Attacker, session.Defender })
        {
            foreach (var group in session.AirPhase.GroupsOf(side))
            {
                if (!wingIds.TryGetValue(group, out var ids))
                {
                    continue;
                }

                for (var i = 0; i < ids.Count && i < group.Wings.Count; i++)
                {
                    if (startWings.TryGetValue(ids[i], out var planes))
                    {
                        group.Wings[i].Planes = planes;
                        wingsById[ids[i]] = (group.Wings[i], group, side);
                    }
                }
            }
        }

        var enabled = session.AirPhase.RegionAirWarEnabled;
        session.AirPhase.RegionAirWarEnabled = true;
        session.AirPhase.RegionAirWar(hour.Hour, new SeededRandomSource(hour.Hour), applyLosses: false);
        session.AirPhase.RegionAirWarEnabled = enabled;

        // [RE + LOG] A-REGION-ESCAPE: an escaped carrier's wings stay in the region's air war, but the logger no longer
        // lists them (their air object moved to group+0x88); they keep losing planes, so take the engine's own losses
        // off the wings the log does not list.
        var listed = wingsById.Values.Select(entry => entry.Wing).ToHashSet();

        foreach (var side in new[] { session.Attacker, session.Defender })
        {
            foreach (var wing in session.AirPhase.GroupsOf(side).Where(group => group.Carrier is not null).SelectMany(group => group.Wings))
            {
                if (!listed.Contains(wing) && wing.RegionLosses > 0)
                {
                    wing.Planes = Math.Max(0, wing.Planes - wing.RegionLosses);
                }
            }
        }

        foreach (var (id, (wing, group, side)) in wingsById)
        {
            if (game.TryGetValue(id, out var logged))
            {
                result.Add(new ReplayRegionWing(hour.Hour, side.Label, group.Source, wing.Plane.ID, id, startWings[id], wing.Disruption, logged.Disruption,
                    Math.Min(wing.RegionLosses, startWings[id]), startWings[id] - logged.Planes));
            }
        }

        return result;
    }

    // The side values the hour starts from (the previous hour's): carrier sorties, and so positioning, read screening.
    private static void ApplySides(BattleSession session, IReadOnlyList<ReplaySideState>? start)
    {
        if (start is null)
        {
            return;
        }

        foreach (var (state, side) in start.Zip(new[] { session.Attacker, session.Defender }))
        {
            side.Positioning = state.Positioning;
            side.Screening = new ScreeningSummary(state.Screening, state.CarrierScreening);
            side.FleetAntiAir = state.FleetAntiAir;
        }
    }

    // S-POS, S-SCREEN, A-AA-REDUCE: the side values of the hour, by side label in place of a ship ID.
    private static IEnumerable<ReplayStateMismatch> CompareSides(IReadOnlyList<ReplaySideState>? game, BattleSession session)
    {
        if (game is null)
        {
            yield break;
        }

        foreach (var (state, side) in game.Zip(new[] { session.Attacker, session.Defender }))
        {
            var values = new (string Field, double Game, double Engine)[]
            {
                ("side positioning", state.Positioning, side.Positioning),
                ("side screening", state.Screening, side.Screening.ScreeningEfficiency),
                ("side carrier screening", state.CarrierScreening, side.Screening.CarrierScreeningEfficiency),
                ("side fleet AA", state.FleetAntiAir, side.FleetAntiAir)
            };

            foreach (var (field, gameValue, engineValue) in values)
            {
                if (Math.Abs(gameValue - engineValue) > FractionTolerance * Math.Max(1, Math.Abs(gameValue)))
                {
                    yield return new ReplayStateMismatch(side.Label, field, gameValue, engineValue);
                }
            }
        }
    }

    private static (int Matching, string? First) CompareAir(IReadOnlyList<ReplayAirStrike> game, IReadOnlyList<AirStrikeRecord> engine)
    {
        var matching = 0;
        string? first = null;

        for (var i = 0; i < Math.Max(game.Count, engine.Count); i++)
        {
            var g = i < game.Count ? game[i] : null;
            var e = i < engine.Count ? engine[i] : null;
            var difference = g is null ? "the game made fewer strikes"
                : e is null ? "the engine made fewer strikes"
                : g.CarrierId is not null && g.CarrierId != e.Source ? "source"
                : g.TargetId != e.TargetId ? "target"
                : g.Intercepted is { } intercepted && intercepted != e.Intercepted ? "intercepted"
                : g.ShotDown is { } shotDown && shotDown != e.ShotDownByAntiAir ? "shot down by AA"
                : Math.Abs(g.HpDamage - e.HpDamage) > DamageTolerance ? "HP damage"
                : Math.Abs(g.OrganizationDamage - e.OrganizationDamage) > DamageTolerance ? "organization damage"
                : null;

            if (difference is null)
            {
                matching++;
            }
            else
            {
                first ??= $"strike {i} differs in {difference}: game {Describe(g)}, engine {Describe(e)}";
            }
        }

        return (matching, first);
    }

    private static string Describe(ReplayAirStrike? strike)
    {
        return strike is null ? "-"
            : $"{strike.CarrierId ?? "land-based"} sortie {strike.Sortie:F5} -> {strike.TargetId}: {strike.Strikers} strikers, " +
              $"{strike.Intercepted} intercepted, {strike.ShotDown} shot down, HP {strike.HpDamage:F3} org {strike.OrganizationDamage:F3}";
    }

    private static string Describe(AirStrikeRecord? strike)
    {
        return strike is null ? "-"
            : $"{strike.Source} -> {strike.TargetId}: {strike.Flying} flying, {strike.Intercepted} intercepted, {strike.ShotDownByAntiAir} shot down, " +
              $"{strike.Hits} hits, AA reduction {strike.AntiAirReduction:F5}, HP {strike.HpDamage:F3} org {strike.OrganizationDamage:F3}";
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
