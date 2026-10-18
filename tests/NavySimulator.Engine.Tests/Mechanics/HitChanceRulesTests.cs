using NavySimulator.Engine.Battles;
using NavySimulator.Engine.Battles.Mechanics;
using NavySimulator.Engine.Defines;
using NavySimulator.Engine.Tests.TestSupport;

namespace NavySimulator.Engine.Tests.Mechanics;

public class HitChanceRulesTests
{
    private readonly GameDefines defines = GameDefines.Vanilla();
    private static readonly ScreeningSummary FullScreening = new(1, 1);

    private static Ship LightCruiser1936()
    {
        return TestEntityFactory.CreateShip("cl", ShipRole.Screen, TestEntityFactory.ScreenStats() with { SurfaceVisibility = 15, Speed = 32 });
    }

    [Fact]
    public void ShipHitProfile_MatchesWikiLightCruiserExample()
    {
        Assert.Equal(41.667, HitChanceRules.ShipHitProfile(LightCruiser1936(), defines), 3);
    }

    [Fact]
    public void ShipHitProfile_FloorsTheSpeedTerm()
    {
        var stopped = TestEntityFactory.CreateShip("dd", ShipRole.Screen, TestEntityFactory.ScreenStats() with { SurfaceVisibility = 15, Speed = 0 });

        Assert.Equal(1500 / 20.1, HitChanceRules.ShipHitProfile(stopped, defines), 6);
    }

    [Fact]
    public void Calculate_SquaresTheProfileRatio()
    {
        var result = HitChanceRules.Calculate(TestEntityFactory.Screen("dd"), LightCruiser1936(), WeaponSlot.Light, FullScreening, 0, defines);

        Assert.Equal(45, result.WeaponHitProfile);
        Assert.Equal(Math.Pow(41.6667 / 45, 2), result.ProfileFactor, 3);
        Assert.Equal(0.1 * result.ProfileFactor, result.Final, 6);
    }

    [Fact]
    public void Calculate_AppliesTheMinimumLast()
    {
        var tiny = TestEntityFactory.CreateShip("tiny", ShipRole.Screen, TestEntityFactory.ScreenStats() with { SurfaceVisibility = 0.5 });

        var result = HitChanceRules.Calculate(TestEntityFactory.Capital("bb"), tiny, WeaponSlot.Heavy, FullScreening, 0, defines);

        Assert.Equal(0.005, result.Final, 9);
    }

    [Fact]
    public void Calculate_HasNoUpperCap()
    {
        var sharpshooter = TestEntityFactory.CreateShip("dd", ShipRole.Screen, TestEntityFactory.ScreenStats() with { LightHitChanceFactor = 20 });

        var result = HitChanceRules.Calculate(sharpshooter, LightCruiser1936(), WeaponSlot.Light, FullScreening, 0, defines);

        Assert.True(result.Final > 1);
    }

    [Fact]
    public void ModifierSum_AddsScreeningAndNightInsteadOfMultiplying()
    {
        var capital = TestEntityFactory.Capital("bb");

        Assert.Equal(1.4, HitChanceRules.ModifierSum(capital, WeaponSlot.Heavy, FullScreening, 0, defines), 9);
        Assert.Equal(1.15, HitChanceRules.ModifierSum(capital, WeaponSlot.Heavy, FullScreening, 1, defines), 9);
        Assert.Equal(1.0, HitChanceRules.ModifierSum(TestEntityFactory.Screen("dd"), WeaponSlot.Light, FullScreening, 0, defines), 9);
    }

    [Fact]
    public void CrewFactor_AddsOrganizationAndManpowerPenalties()
    {
        var ship = TestEntityFactory.Screen("dd");
        ship.CurrentOrganization = 0;
        ship.CurrentManpower = ship.MaxManpower / 2;

        Assert.Equal(1 - 0.5 - 0.25 * 0.5, HitChanceRules.CrewFactor(ship, defines), 9);
    }

    [Fact]
    public void Calculate_DepthChargesUseTheirOwnProfileAndMultiplier()
    {
        var submarine = TestEntityFactory.Submarine("ss");

        var result = HitChanceRules.Calculate(TestEntityFactory.Screen("dd"), submarine, WeaponSlot.Light, FullScreening, 0, defines);

        Assert.Equal(100, result.WeaponHitProfile);
        Assert.Equal(0.1 * 1.1, result.BaseChance, 9);
        Assert.Equal(1000 / 27.5, result.ShipHitProfile, 6);
    }
}
