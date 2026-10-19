using NavySimulator.Web.Services;

namespace NavySimulator.Web.Tests;

public sealed class FleetCostTests : IDisposable
{
    private readonly TestWorkspace test = new();

    public void Dispose()
    {
        test.Dispose();
    }

    [Fact]
    public void Of_CountsSunkShipsWholeAndTheOthersHpDamageAtTheirCost()
    {
        var cost = FleetCost.Of([(1000.0, 100.0, 0.0), (500.0, 50.0, 25.0), (200.0, 40.0, 40.0)]);

        Assert.Equal(3, cost.Ships);
        Assert.Equal(1700, cost.Total);
        Assert.Equal(1000, cost.Sunk);
        Assert.Equal(250, cost.Damaged);
        Assert.Equal(1000 / 1700.0, cost.SunkShare);
    }

    [Fact]
    public void Of_BuiltScenario_IsTheShipsProductionCost()
    {
        var ships = test.Workspace.BuildScenario().Scenario!.Attacker.Fleet.Ships;

        var cost = FleetCost.Of(ships);

        Assert.Equal(ships.Count, cost.Ships);
        Assert.Equal(ships.Sum(ship => ship.GetFinalStats().ProductionCost), cost.Total, 6);
        Assert.True(cost.Total > 0);
        Assert.Equal(0, cost.Sunk);
        Assert.Equal(0, cost.Damaged);
    }

    [Fact]
    public async Task BattleView_CostMatchesTheScenarioAndTheEngineResult()
    {
        var runner = new BattleRunner(test.Workspace);
        runner.Start(2);
        var scenario = test.Workspace.BuildScenario().Scenario!;

        Assert.Equal(FleetCost.Of(scenario.Defender.Fleet.Ships), runner.View()!.Defender.Cost);

        await runner.RunToEndAsync();
        var view = runner.View()!;
        var result = runner.Result()!;

        foreach (var side in view.Sides)
        {
            var expected = FleetCost.Of(result, side.Label);
            Assert.Equal(expected.Total, side.Cost.Total, 6);
            Assert.Equal(expected.Sunk, side.Cost.Sunk, 6);
            Assert.Equal(expected.Damaged, side.Cost.Damaged, 6);
        }

        Assert.True(view.Sides.Any(side => side.Cost.Sunk > 0), "seed 2 sinks ships");
    }
}
