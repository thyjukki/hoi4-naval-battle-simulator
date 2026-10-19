using NavySimulator.Engine.Battles;
using NavySimulator.Engine.Results;
using NavySimulator.Engine.Tests.TestSupport;

namespace NavySimulator.Engine.Tests.Battles;

public class BattleSessionTests
{
    private static BattleScenario MixedScenario(BattleRules? rules = null)
    {
        var attacker = Enumerable.Range(1, 6).Select(i => TestEntityFactory.Screen($"a{i:D2}", 6)).Append(TestEntityFactory.Capital("a_bb"));
        var defender = Enumerable.Range(1, 5).Select(i => TestEntityFactory.Screen($"d{i:D2}", 6)).Append(TestEntityFactory.Capital("d_bb"));
        return TestEntityFactory.Scenario(attacker, defender, rules ?? new BattleRules { MaxHours = 300 });
    }

    [Fact]
    public void SteppingInChunks_GivesTheSameBattleAsSimulate()
    {
        var expected = new BattleEngine().Simulate(MixedScenario(), 17);
        var session = new BattleSession(MixedScenario(), 17);

        session.StepHour();
        session.Step(10);
        session.Step(3);
        session.RunToEnd();
        var actual = session.BuildResult();

        Assert.Equal(expected.Outcome, actual.Outcome);
        Assert.Equal(expected.HoursFought, actual.HoursFought);
        Assert.Equal(expected.Shots, actual.Shots);
        Assert.Equal(expected.Ships, actual.Ships);
        Assert.Equal(expected.HourlyLog, actual.HourlyLog);
        Assert.Equal(expected.Events, actual.Events);
    }

    [Fact]
    public void ResultBeforeTheEnd_IsInProgress_AndLaterStepsDoNotChangeIt()
    {
        var session = new BattleSession(MixedScenario(), 3);

        session.Step(5);
        var partial = session.BuildResult();
        var shotsAtHourFive = partial.Shots.Count;
        session.RunToEnd();

        Assert.Equal(BattleOutcome.InProgress, partial.Outcome);
        Assert.Equal(5, partial.HoursFought);
        Assert.Equal(shotsAtHourFive, partial.Shots.Count);
        Assert.NotEqual(BattleOutcome.InProgress, session.Outcome);
        Assert.True(session.IsOver);
    }

    [Fact]
    public void StepAfterTheEnd_DoesNothing()
    {
        var session = new BattleSession(MixedScenario(), 3);
        session.RunToEnd();
        var hours = session.HoursFought;

        Assert.False(session.StepHour());
        Assert.Equal(0, session.Step(10));
        Assert.Equal(hours, session.HoursFought);
    }

    [Fact]
    public void Manpower_IsRefilledOnlyAfterTheHourAtMidnight()
    {
        // Unarmed ships take no losses, so every change comes from the daily refill.
        var attacker = new[] { TestEntityFactory.Screen("a1", lightAttack: 0) };
        var defender = new[] { TestEntityFactory.Screen("d1", lightAttack: 0) };
        var session = new BattleSession(TestEntityFactory.Scenario(attacker, defender, new BattleRules { StartHourOfDay = 22, MaxHours = 30 }), 1);
        attacker[0].CurrentManpower = 100;

        session.Step(2);
        Assert.Equal(100, attacker[0].CurrentManpower);

        session.StepHour();
        Assert.Equal(120, attacker[0].CurrentManpower);

        session.Step(23);
        Assert.Equal(120, attacker[0].CurrentManpower);

        session.StepHour();
        Assert.Equal(140, attacker[0].CurrentManpower);
    }

    [Fact]
    public void Night_NeedsWeather()
    {
        // S-NIGHT [CAL]: with weather off the game's darkness stays 0 all day.
        var withWeather = new BattleSession(MixedScenario(new BattleRules { StartHourOfDay = 22 }), 1);
        var withoutWeather = new BattleSession(MixedScenario(new BattleRules { StartHourOfDay = 22, WeatherEnabled = false }), 1);

        Assert.True(withWeather.IsNight(0));
        Assert.False(withoutWeather.IsNight(0));
    }

    [Fact]
    public void MaxHoursEndsTheBattleAsTimeout()
    {
        var session = new BattleSession(MixedScenario(new BattleRules { MaxHours = 2 }), 3);

        Assert.Equal(2, session.Step(100));
        Assert.True(session.IsOver);
        Assert.Equal(BattleOutcome.Timeout, session.Outcome);
    }

    [Fact]
    public void Timeline_HasOneEntryPerHour_WithTheHoursFire()
    {
        var session = new BattleSession(MixedScenario(), 8);
        session.RunToEnd();

        Assert.Equal(session.HoursFought, session.Timeline.Count);
        Assert.Equal(Enumerable.Range(0, session.HoursFought), session.Timeline.Select(hour => hour.Hour));

        foreach (var hour in session.Timeline)
        {
            var hourShots = session.Shots.Where(shot => shot.Hour == hour.Hour).ToList();
            Assert.Equal(hourShots.Count(shot => shot.ShooterSide == "attacker"), hour.Attacker.ShotsFired);
            Assert.Equal(hourShots.Count(shot => shot.ShooterSide == "defender" && shot.Hit), hour.Defender.Hits);
            Assert.Equal(hourShots.Where(shot => shot.ShooterSide == "attacker").Sum(shot => shot.HpDamage), hour.Attacker.HpDamageDealt, 6);
        }

        var last = session.Timeline[^1];
        Assert.Equal(session.SideResultOf(session.Attacker).ShipsSunk, last.Attacker.ShipsSunk);
        Assert.Equal(session.SideResultOf(session.Defender).ShipsSunk, last.Defender.ShipsSunk);
    }

    [Fact]
    public void ShipHistory_RecordsEveryShipAtTheEndOfEachHour()
    {
        var session = new BattleSession(MixedScenario(), 8, recordShipHistory: true);
        session.RunToEnd();
        var lastHour = session.HoursFought - 1;

        for (var index = 0; index < session.Ships.Count; index++)
        {
            var ship = session.Ships[index];
            var state = session.ShipStateAt(lastHour, index);
            Assert.Equal(ship.CurrentHP, state.Hp);
            Assert.Equal(ship.CurrentStatus, state.Status);

            var sunkHour = session.ShipResultOf(ship).SunkHour;

            if (sunkHour is { } hour)
            {
                Assert.True(session.ShipStateAt(hour, index).Sunk);
                Assert.True(hour == 0 || !session.ShipStateAt(hour - 1, index).Sunk);
            }
        }
    }

    [Fact]
    public void ShipHistory_IsOffByDefault()
    {
        var session = new BattleSession(MixedScenario(), 8);
        session.StepHour();

        Assert.False(session.RecordsShipHistory);
        Assert.Throws<InvalidOperationException>(() => session.ShipStateAt(0, 0));
    }

    [Fact]
    public void Events_NameEverySunkShipOnceWithItsKiller()
    {
        var session = new BattleSession(MixedScenario(new BattleRules { MaxHours = 300, RetreatEnabled = false }), 21);
        session.RunToEnd();
        var result = session.BuildResult();
        var sunkEvents = result.Events.Where(battleEvent => battleEvent.Kind == BattleEventKind.Sunk).ToList();
        var killingBlows = result.Shots.Where(shot => shot.KillingBlow).ToList();

        Assert.NotEmpty(killingBlows);
        Assert.Equal(killingBlows.Select(shot => (shot.Hour, shot.TargetId, shot.ShooterId)),
            sunkEvents.Select(battleEvent => (battleEvent.Hour, battleEvent.ShipId!, battleEvent.OtherShipId!)));
        Assert.Equal(result.Ships.Count(ship => ship.Sunk), sunkEvents.Count);
    }

    [Fact]
    public void Events_ActivateEveryShipThatFired()
    {
        var session = new BattleSession(MixedScenario(), 4);
        session.RunToEnd();

        var activated = session.Events.Where(battleEvent => battleEvent.Kind == BattleEventKind.Activated)
            .ToDictionary(battleEvent => battleEvent.ShipId!, battleEvent => battleEvent.Hour);

        foreach (var shot in session.Shots)
        {
            Assert.True(activated[shot.ShooterId] < shot.Hour, $"{shot.ShooterId} fired at hour {shot.Hour} before activating");
        }
    }

    [Fact]
    public void ASinking_RefillsTheRestOfTheTaskForce()
    {
        var weak = TestEntityFactory.CreateShip("d_weak", ShipRole.Screen, TestEntityFactory.ScreenStats() with { Hp = 1 });
        var other = TestEntityFactory.Screen("d_other");
        var scenario = TestEntityFactory.Scenario([TestEntityFactory.Capital("a_bb")], [weak, other], new BattleRules { MaxHours = 200 });
        var session = new BattleSession(scenario, 5);

        session.Step(1);
        other.CurrentManpower = other.MaxManpower / 2;

        while (!weak.IsSunk && session.StepHour())
        {
        }

        Assert.True(weak.IsSunk);
        Assert.Contains(session.HourlyLog, line => line.Contains("defender ship sunk, the task force regains 20 manpower"));
    }
}
