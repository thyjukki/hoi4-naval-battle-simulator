using NavySimulator.Engine.Battles;
using NavySimulator.Engine.Battles.Mechanics;
using NavySimulator.Engine.Defines;
using NavySimulator.Engine.Tests.TestSupport;

namespace NavySimulator.Engine.Tests.Mechanics;

public class PositioningAndScreeningTests
{
    private readonly GameDefines defines = GameDefines.Vanilla();

    private static BattleLines Screens(int count)
    {
        return new BattleLines(Enumerable.Range(0, count).Select(i => TestEntityFactory.Screen($"dd{i}")));
    }

    [Theory]
    [InlineData(3, 2, 0)]
    [InlineData(10, 10, 0)]
    [InlineData(10, 5, 0.25)]
    [InlineData(40, 5, 0.75)]
    public void FleetSizePenalty_ScalesWithShipRatioAboveMinimumFleetSize(int own, int enemy, double expected)
    {
        Assert.Equal(expected, PositioningRules.FleetSizePenalty(own, enemy, defines), 6);
    }

    [Fact]
    public void Calculate_LargerFleetLosesPositioning()
    {
        Assert.Equal(0.75, PositioningRules.Calculate(Screens(10), Screens(5), defines), 6);
        Assert.Equal(1.0, PositioningRules.Calculate(Screens(5), Screens(10), defines), 6);
    }

    [Fact]
    public void Screening_RequiresThreeScreensPerCapital_AndHalvesAtZeroPositioning()
    {
        var lines = new BattleLines([TestEntityFactory.Screen("dd1"), TestEntityFactory.Screen("dd2"), TestEntityFactory.Capital("bb")]);

        Assert.Equal(2.0 / 3.0, ScreeningRules.Calculate(lines, 1.0, defines).ScreeningEfficiency, 6);
        Assert.Equal(1.0 / 3.0, ScreeningRules.Calculate(lines, 0.0, defines).ScreeningEfficiency, 6);
    }

    [Fact]
    public void CarrierScreening_RequiresOneCapitalPerCarrier()
    {
        var carrier1 = TestEntityFactory.CreateShip("cv1", ShipRole.Carrier, TestEntityFactory.CapitalStats());
        var carrier2 = TestEntityFactory.CreateShip("cv2", ShipRole.Carrier, TestEntityFactory.CapitalStats());
        var lines = new BattleLines([carrier1, carrier2, TestEntityFactory.Capital("bb")]);

        Assert.Equal(0.5, ScreeningRules.Calculate(lines, 1.0, defines).CarrierScreeningEfficiency, 6);
    }
}
