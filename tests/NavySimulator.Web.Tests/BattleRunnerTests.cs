using NavySimulator.Engine;
using NavySimulator.Engine.Battles;
using NavySimulator.Engine.Results;
using NavySimulator.Web.Services;

namespace NavySimulator.Web.Tests;

public sealed class BattleRunnerTests : IDisposable
{
    private readonly TestWorkspace test = new();

    public void Dispose()
    {
        test.Dispose();
    }

    [Fact]
    public async Task Stepping_AdvancesTheShownHour()
    {
        var runner = new BattleRunner(test.Workspace);

        Assert.True(runner.Start(5));
        Assert.Equal(-1, runner.View()!.Hour);

        await runner.StepAsync(1);
        await runner.StepAsync(10);

        var view = runner.View()!;
        Assert.Equal(11, view.HoursFought);
        Assert.Equal(10, view.Hour);
        Assert.True(view.IsLive);
        Assert.Equal(BattleOutcome.InProgress, view.Outcome);
    }

    [Fact]
    public async Task RunToEnd_GivesTheSameBattleAsTheEngine()
    {
        var runner = new BattleRunner(test.Workspace);
        runner.Start(9);

        await runner.StepAsync(3);
        await runner.RunToEndAsync();
        var expected = new BattleEngine().Simulate(test.Workspace.BuildScenario().Scenario!, 9);
        var actual = runner.Result()!;

        Assert.Equal(expected.Outcome, actual.Outcome);
        Assert.Equal(expected.HoursFought, actual.HoursFought);
        Assert.Equal(expected.Shots, actual.Shots);
        Assert.True(runner.View()!.IsOver);
    }

    [Fact]
    public async Task LiveView_ShipTotalsMatchTheEngine()
    {
        var runner = new BattleRunner(test.Workspace);
        runner.Start(2);
        await runner.StepAsync(40);

        var view = runner.View()!;
        var result = runner.Result()!;

        foreach (var ship in result.Ships)
        {
            var row = view.FindShip(ship.ShipId)!;
            Assert.Equal(ship.ShotsFired, row.ShotsFired);
            Assert.Equal(ship.Hits, row.Hits);
            Assert.Equal(ship.HpDamageDealt, row.HpDamageDealt, 6);
            Assert.Equal(ship.HpDamageTaken, row.HpDamageTaken, 6);
            Assert.Equal(ship.FinalHp, row.Hp);
            Assert.Equal(ship.FinalStatus, row.Status);
            Assert.Equal(ship.SunkHour, row.SunkHour);
        }
    }

    [Fact]
    public async Task RewoundView_ShowsTheShipsAsTheyWereThen()
    {
        var runner = new BattleRunner(test.Workspace);
        runner.Start(2);
        await runner.StepAsync(10);
        var atFour = runner.View(4)!;
        await runner.StepAsync(30);

        var rewound = runner.View(4)!;

        Assert.False(rewound.IsLive);
        Assert.Equal(4, rewound.Hour);
        Assert.All(rewound.Sides.SelectMany(side => side.Ships), ship =>
        {
            var before = atFour.FindShip(ship.Id)!;
            Assert.Equal(before.Hp, ship.Hp);
            Assert.Equal(before.ShotsFired, ship.ShotsFired);
            Assert.Equal(before.HpDamageTaken, ship.HpDamageTaken, 6);
        });
        Assert.Equal(rewound.Shots.Count(shot => shot.Hour <= 4), atFour.Shots.Count(shot => shot.Hour <= 4));
    }

    [Fact]
    public void BeforeTheFirstHour_EveryShipIsWaitingAtFullHp()
    {
        var runner = new BattleRunner(test.Workspace);
        runner.Start(1);

        var view = runner.View()!;

        Assert.All(view.Sides.SelectMany(side => side.Ships), ship =>
        {
            Assert.Equal(ShipStatus.Waiting, ship.Status);
            Assert.Equal(ship.Stats.Hp, ship.Hp);
        });
    }

    [Fact]
    public void ShipsAreGroupedByDesign_AndNumbered()
    {
        var runner = new BattleRunner(test.Workspace);
        runner.Start(1);

        var attacker = runner.View()!.Attacker;

        Assert.All(attacker.Designs, design => Assert.All(design.Ships, ship => Assert.Equal(design.DesignId, ship.DesignId)));
        Assert.Equal("#1", attacker.Designs[0].Ships[0].Number);
        Assert.Equal(attacker.Count, attacker.Designs.Sum(design => design.Count));
    }

    [Fact]
    public void EditingTheScenario_MarksTheBattleOutdated()
    {
        var runner = new BattleRunner(test.Workspace);
        runner.Start(1);

        Assert.False(runner.IsOutdated);

        test.Workspace.Scenario.Attacker.ShipExperienceLevel = 4;

        Assert.True(runner.IsOutdated);
    }

    [Fact]
    public void Start_WithAnInvalidScenario_ReportsErrors()
    {
        var runner = new BattleRunner(test.Workspace);
        test.Workspace.Scenario.Attacker.FleetID = "nope";

        Assert.False(runner.Start(1));
        Assert.NotEmpty(runner.StartErrors);
        Assert.False(runner.HasBattle);
    }

    [Fact]
    public async Task ShipLog_HasEveryShotOfTheShip_InHourOrder()
    {
        var runner = new BattleRunner(test.Workspace);
        runner.Start(3);
        await runner.RunToEndAsync();
        var view = runner.View()!;
        var ship = view.Sides.SelectMany(side => side.Ships).First(candidate => candidate.Sunk);

        var log = ShipLog.Build(view, ship.Id);

        Assert.Equal(ship.ShotsFired, log.Count(entry => entry.Kind == ShipLogKind.Fired));
        Assert.Equal(view.Shots.Count(shot => shot.TargetId == ship.Id), log.Count(entry => entry.Kind == ShipLogKind.Received));
        Assert.Equal(log.OrderBy(entry => entry.Hour).Select(entry => entry.Hour), log.Select(entry => entry.Hour));
        Assert.Equal(BattleEventKind.Activated, log[0].EventKind);
        Assert.Equal(BattleEventKind.Sunk, log[^1].EventKind);
    }

    [Fact]
    public async Task ShotFilter_FiltersBySideWeaponHitsAndHours()
    {
        var runner = new BattleRunner(test.Workspace);
        runner.Start(3);
        await runner.StepAsync(30);
        var view = runner.View()!;

        var filter = new ShotFilter { Side = "defender", Weapon = "Light", HitsOnly = true, FromHour = 5, ToHour = 9 };
        var shots = filter.Apply(view);

        Assert.NotEmpty(shots);
        Assert.All(shots, shot =>
        {
            Assert.Equal("defender", shot.ShooterSide);
            Assert.Equal(WeaponType.Light, shot.Weapon);
            Assert.True(shot.Hit);
            Assert.InRange(shot.Hour, 5, 9);
        });

        var byName = new ShotFilter { Search = "#7" }.Apply(view);
        Assert.All(byName, shot => Assert.True(view.NameOf(shot.ShooterId).Contains("#7") || view.NameOf(shot.TargetId).Contains("#7")));
    }
}
