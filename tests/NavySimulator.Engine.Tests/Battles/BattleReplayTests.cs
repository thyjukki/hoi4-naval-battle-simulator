using NavySimulator.Engine.Battles;
using NavySimulator.Engine.Battles.Replay;
using NavySimulator.Engine.Random;
using NavySimulator.Engine.Tests.TestSupport;

namespace NavySimulator.Engine.Tests.Battles;

public class BattleReplayTests
{
    private static BattleScenario Scenario()
    {
        var attacker = Enumerable.Range(1, 6).Select(i => TestEntityFactory.Screen($"a{i:D2}", 6, 4)).Append(TestEntityFactory.Capital("a_bb"));
        var defender = Enumerable.Range(1, 5).Select(i => TestEntityFactory.Screen($"d{i:D2}", 6)).Append(TestEntityFactory.Capital("d_bb"));
        return TestEntityFactory.Scenario(attacker, defender, new BattleRules { MaxHours = 120 });
    }

    private static ReplayShipState StateOf(Ship ship)
    {
        return new ReplayShipState(ship.CurrentStatus, ship.IsSunk, ship.CurrentHP, ship.CurrentOrganization, ship.CurrentManpower,
            ship.Experience, ship.RetreatProgress, ship.SlotCharge.ToArray());
    }

    // An engine battle written down the way the in-game logger writes one: start and end states, rolls and shots per hour.
    private static List<ReplayHour> Record(BattleScenario scenario, int seed)
    {
        var random = new RecordingRandomSource(seed);
        var session = new BattleSession(scenario, random);
        var hours = new List<ReplayHour>();

        while (!session.IsOver)
        {
            var hour = session.HoursFought;
            var start = session.Ships.ToDictionary(ship => ship.ID, StateOf);
            var rollsBefore = random.Rolls.Count;
            var shotsBefore = session.Shots.Count;
            session.StepHour();
            var shots = session.Shots.Skip(shotsBefore)
                .Select(shot => new ReplayShot(shot.ShooterId, shot.TargetId, shot.Weapon, shot.Hit, shot.HpDamage, shot.OrganizationDamage))
                .ToList();
            hours.Add(new ReplayHour(hour, start, random.Rolls.Skip(rollsBefore).ToList(), shots,
                session.Ships.ToDictionary(ship => ship.ID, StateOf)));
        }

        return hours;
    }

    [Fact]
    public void ARecordedEngineBattle_ReplaysExactly()
    {
        var hours = Record(Scenario(), 11);

        var reports = BattleReplay.Run(Scenario(), hours);

        Assert.Equal(hours.Count, reports.Count);
        Assert.All(reports, report => Assert.True(report.Matches, $"hour {report.Hour}: {report.Divergence} {report.FirstShotMismatch} {report.StateMismatches.Count}"));
        Assert.True(reports.Sum(report => report.GameShots) > 50);
    }

    [Fact]
    public void ADifferentRoll_IsReportedWhereItHappens()
    {
        var hours = Record(Scenario(), 11);
        var hour = hours.First(entry => entry.Shots.Count > 0);
        var rolls = hour.Rolls.ToList();
        var hit = rolls.FindIndex(roll => roll.Kind == RollKind.Hit);
        rolls.Insert(hit, new ReplayRoll(RollKind.RetreatDecision, 0.5, 0xa83));
        hours[hours.IndexOf(hour)] = hour with { Rolls = rolls };

        var report = BattleReplay.Run(Scenario(), hours).Single(entry => entry.Hour == hour.Hour);

        Assert.Equal(new ReplayDivergence(hit, RollKind.Hit, new ReplayRoll(RollKind.RetreatDecision, 0.5, 0xa83)), report.Divergence);
    }

    [Fact]
    public void ReplaySource_SkipsKindsTheEngineDoesNotRoll_AndReportsLeftovers()
    {
        var source = new ReplayRandomSource(new HashSet<RollKind> { RollKind.PartPick });
        source.Load([
            new ReplayRoll(RollKind.Hit, 0.25, 0x587),
            new ReplayRoll(RollKind.PartPick, 0.5, 0x78f),
            new ReplayRoll(RollKind.TargetPick, 0.75, 0x37b),
            new ReplayRoll(RollKind.Hit, 0.125, 0x587)
        ]);

        Assert.Equal(0.25, source.NextDouble(RollKind.Hit));
        Assert.Equal(0.75, source.NextDouble(RollKind.TargetPick));
        source.Finish();

        Assert.Equal(1, source.Skipped);
        Assert.Equal(new ReplayDivergence(3, null, new ReplayRoll(RollKind.Hit, 0.125, 0x587)), source.Divergence);
    }
}
