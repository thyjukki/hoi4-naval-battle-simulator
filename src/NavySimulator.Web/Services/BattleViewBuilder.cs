using NavySimulator.Engine;
using NavySimulator.Engine.Battles;
using NavySimulator.Engine.Results;

namespace NavySimulator.Web.Services;

/// <summary>Builds a <see cref="BattleView"/> from a session that records ship history. Not thread-safe: hold the session's lock.</summary>
public static class BattleViewBuilder
{
    /// <param name="atHour">The battle hour to show; null or anything past the last fought hour shows the current state.</param>
    public static BattleView Build(BattleSession session, int? atHour = null)
    {
        var lastHour = session.HoursFought - 1;
        var hour = Math.Clamp(atHour ?? lastHour, -1, lastHour);
        var shots = session.Shots.ToArray();
        var airStrikes = session.AirStrikes.ToArray();
        var events = session.Events.ToArray();
        var timeline = session.Timeline.ToArray();
        var tallies = Tally(shots, airStrikes, events, hour);
        var rows = new List<ShipRow>(session.Ships.Count);

        for (var index = 0; index < session.Ships.Count; index++)
        {
            var ship = session.Ships[index];
            var state = hour >= 0
                ? session.ShipStateAt(hour, index)
                : new ShipHourState(ship.GetFinalStats().Hp, ship.GetFinalStats().Organization, ShipStatus.Waiting, ship.InitialExperience);
            var tally = tallies.GetValueOrDefault(ship.ID) ?? new Totals();
            rows.Add(new ShipRow(
                index,
                session.SideOf(ship),
                ship.ID,
                Number(ship, session.SideOf(ship) == session.Attacker.Label ? session.Attacker : session.Defender),
                ship.Design.ID,
                ship.Role,
                ship.GetFinalStats(),
                state.Hp,
                state.Organization,
                state.Experience,
                state.Status,
                tally.SunkHour,
                tally.EscapedHour,
                tally.Shots,
                tally.Hits,
                tally.HpDealt,
                tally.OrgDealt,
                tally.HpTaken,
                tally.Kills));
        }

        var hourSummary = hour >= 0 ? timeline[hour] : null;
        var shipsById = rows.ToDictionary(row => row.Id, StringComparer.Ordinal);

        return new BattleView
        {
            ScenarioId = session.Scenario.ID,
            Seed = session.Seed,
            HoursFought = session.HoursFought,
            IsOver = session.IsOver,
            Outcome = session.Outcome,
            StartHourOfDay = session.HourOfDay(0),
            Hour = hour,
            Attacker = BuildSide(session.Attacker, rows, hourSummary?.Attacker, DamageBreakdownBuilder.Build(session.Attacker.Label, shots, airStrikes, shipsById, hour)),
            Defender = BuildSide(session.Defender, rows, hourSummary?.Defender, DamageBreakdownBuilder.Build(session.Defender.Label, shots, airStrikes, shipsById, hour)),
            Shots = shots,
            AirStrikes = airStrikes,
            Events = events,
            Timeline = timeline,
            HourlyLog = session.HourlyLog.ToArray(),
            Notes = session.Notes.ToArray(),
            SkipCounts = new Dictionary<string, int>(session.SkipCounts, StringComparer.Ordinal),
            ShipsById = shipsById
        };
    }

    private static SideView BuildSide(BattleSide side, List<ShipRow> rows, SideHourState? state, DamageBreakdown damageDealt)
    {
        var designs = rows
            .Where(row => row.Side == side.Label)
            .GroupBy(row => row.DesignId)
            .Select(group => new DesignGroup(side.Label, group.Key, group.First().Role, group.ToList()))
            .OrderBy(group => group.Role)
            .ThenBy(group => group.DesignId, StringComparer.Ordinal)
            .ToList();

        return new SideView(
            side.Label,
            side.Participant.Fleet.ID,
            side.Participant.Aggressiveness,
            side.Participant.RepairPriority,
            designs,
            state?.Positioning ?? 1,
            state?.ScreeningEfficiency ?? 1,
            state?.CarrierScreeningEfficiency ?? 1,
            damageDealt);
    }

    // Ship IDs are "<fleet>_<design>_<n>"; the number is what tells ships of a design apart.
    private static string Number(Ship ship, BattleSide side)
    {
        var prefix = $"{side.Participant.Fleet.ID}_{ship.Design.ID}_";
        return ship.ID.StartsWith(prefix, StringComparison.Ordinal) ? "#" + ship.ID[prefix.Length..].TrimStart('0').PadLeft(1, '0') : ship.ID;
    }

    private static Dictionary<string, Totals> Tally(ShotRecord[] shots, AirStrikeRecord[] airStrikes, BattleEvent[] events, int hour)
    {
        var totals = new Dictionary<string, Totals>(StringComparer.Ordinal);

        Totals Of(string id)
        {
            if (!totals.TryGetValue(id, out var total))
            {
                total = new Totals();
                totals[id] = total;
            }

            return total;
        }

        foreach (var shot in shots)
        {
            if (shot.Hour > hour)
            {
                break;
            }

            var shooter = Of(shot.ShooterId);
            shooter.Shots++;

            if (shot.Hit)
            {
                shooter.Hits++;
                shooter.HpDealt += shot.HpDamage;
                shooter.OrgDealt += shot.OrganizationDamage;
                Of(shot.TargetId).HpTaken += shot.HpDamage;
            }

            if (shot.KillingBlow)
            {
                shooter.Kills++;
            }
        }

        foreach (var strike in airStrikes.Where(strike => strike.Hour <= hour))
        {
            var source = Of(strike.Source);
            source.HpDealt += strike.HpDamage;
            source.OrgDealt += strike.OrganizationDamage;
            source.Kills += strike.KillingBlow ? 1 : 0;
            Of(strike.TargetId).HpTaken += strike.HpDamage;
        }

        foreach (var battleEvent in events.Where(battleEvent => battleEvent.Hour <= hour))
        {
            switch (battleEvent.Kind)
            {
                case BattleEventKind.Sunk:
                    Of(battleEvent.ShipId!).SunkHour = battleEvent.Hour;
                    break;
                case BattleEventKind.Escaped:
                    Of(battleEvent.ShipId!).EscapedHour = battleEvent.Hour;
                    break;
                case BattleEventKind.Reengaged:
                    foreach (var total in totals.Values)
                    {
                        total.EscapedHour = null;
                    }

                    break;
            }
        }

        return totals;
    }

    private sealed class Totals
    {
        public int Shots;
        public int Hits;
        public double HpDealt;
        public double OrgDealt;
        public double HpTaken;
        public int Kills;
        public int? SunkHour;
        public int? EscapedHour;
    }
}
