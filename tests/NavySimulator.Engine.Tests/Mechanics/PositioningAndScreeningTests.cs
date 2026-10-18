using NavySimulator.Engine.Battles;
using NavySimulator.Engine.Battles.Mechanics;
using NavySimulator.Engine.Defines;
using NavySimulator.Engine.Tests.TestSupport;

namespace NavySimulator.Engine.Tests.Mechanics;

public class PositioningAndScreeningTests
{
    private readonly GameDefines defines = GameDefines.Vanilla();

    private static BattleLines Screens(int count, double detection = 20)
    {
        return new BattleLines(Enumerable.Range(0, count).Select(i =>
            TestEntityFactory.CreateShip($"dd{i}", ShipRole.Screen, TestEntityFactory.ScreenStats() with { SurfaceDetection = detection })));
    }

    [Theory]
    [InlineData(3, 2, 0)]
    [InlineData(10, 10, 0)]
    [InlineData(10, 5, 0.25)]
    [InlineData(40, 5, 0.75)]
    [InlineData(4, 8, 0)]
    public void FleetSizePenalty_ScalesWithShipRatioAboveMinimumFleetSize(int own, int enemy, double expected)
    {
        Assert.Equal(expected, PositioningRules.FleetSizePenalty(own, enemy, defines), 9);
    }

    [Fact]
    public void DetectionBonus_UsesSummedDetectionDividedByHundred()
    {
        // Ten ships at 40 detection against ten at 20: (400 - 200) / 100 * 0.01 = 0.02.
        Assert.Equal(0.02, PositioningRules.DetectionBonus(Screens(10, 40), Screens(10, 20), defines), 9);
        Assert.Equal(0, PositioningRules.DetectionBonus(Screens(10, 20), Screens(10, 40), defines), 9);
    }

    [Fact]
    public void Calculate_DetectionBonusOffsetsTheSizePenalty()
    {
        Assert.Equal(0.75 + 0.01, PositioningRules.Calculate(Screens(10, 30), Screens(5, 40), defines), 9);
    }

    [Fact]
    public void Screening_RoundsTheRequiredShipsUp()
    {
        // One capital and one convoy need ceil(3 + 0.5) = 4 screens.
        var convoy = TestEntityFactory.CreateShip("co", ShipRole.Convoy, TestEntityFactory.ScreenStats());
        var lines = new BattleLines([TestEntityFactory.Screen("dd1"), TestEntityFactory.Screen("dd2"), TestEntityFactory.Capital("bb"), convoy]);

        Assert.Equal(0.5, ScreeningRules.Calculate(lines, 1.0, defines).ScreeningEfficiency, 9);
    }

    [Fact]
    public void Screening_ShrinksWithPositioning()
    {
        var lines = new BattleLines([TestEntityFactory.Screen("dd1"), TestEntityFactory.Screen("dd2"), TestEntityFactory.Capital("bb")]);

        Assert.Equal(2.0 / 3.0, ScreeningRules.Calculate(lines, 1.0, defines).ScreeningEfficiency, 9);
        Assert.Equal(1.0 / 3.0, ScreeningRules.Calculate(lines, 0.0, defines).ScreeningEfficiency, 9);
    }

    [Fact]
    public void CarrierScreening_RequiresOneCapitalPerCarrier()
    {
        var lines = new BattleLines([TestEntityFactory.Carrier("cv1"), TestEntityFactory.Carrier("cv2"), TestEntityFactory.Capital("bb")]);

        Assert.Equal(0.5, ScreeningRules.Calculate(lines, 1.0, defines).CarrierScreeningEfficiency, 9);
    }
}
