using NavySimulator.Engine.Battles;
using NavySimulator.Engine.Battles.Mechanics;
using NavySimulator.Engine.Defines;
using NavySimulator.Engine.Tests.TestSupport;

namespace NavySimulator.Engine.Tests.Mechanics;

public class HitChanceRulesTests
{
    private readonly GameDefines defines = GameDefines.Vanilla();

    [Fact]
    public void ShipHitProfile_MatchesWikiLightCruiserExample()
    {
        // Wiki: a 1936 light cruiser with 15 surface visibility and 32 kn has a hit profile of ca. 41.7.
        var cruiser = TestEntityFactory.CreateShip("cl", ShipRole.Screen, TestEntityFactory.ScreenStats() with { SurfaceVisibility = 15, Speed = 32 });

        Assert.Equal(41.667, HitChanceRules.ShipHitProfile(cruiser, defines), 3);
    }

    [Fact]
    public void Calculate_AppliesProfileRatioSquared_ForLightGuns()
    {
        var shooter = TestEntityFactory.Screen("shooter");
        var target = TestEntityFactory.CreateShip("cl", ShipRole.Screen, TestEntityFactory.ScreenStats() with { SurfaceVisibility = 15, Speed = 32 });

        var result = HitChanceRules.Calculate(shooter, target, WeaponType.Light, new ScreeningSummary(1, 1), false, defines, new BattleRules());

        Assert.Equal(45, result.WeaponHitProfile);
        Assert.Equal(Math.Pow(41.6667 / 45, 2), result.ProfileFactor, 3);
        Assert.Equal(0.1 * result.ProfileFactor, result.Final, 6);
    }

    [Fact]
    public void Calculate_NeverGoesBelowMinimumHitChance()
    {
        var shooter = TestEntityFactory.Screen("shooter");
        var target = TestEntityFactory.CreateShip("tiny", ShipRole.Screen, TestEntityFactory.ScreenStats() with { SurfaceVisibility = 0.5 });

        var result = HitChanceRules.Calculate(shooter, target, WeaponType.Heavy, new ScreeningSummary(1, 1), false, defines, new BattleRules());

        Assert.Equal(defines.NNavy.COMBAT_MIN_HIT_CHANCE, result.Final);
    }

    [Fact]
    public void Modifier_GivesScreeningBonusOnlyOnce_ToBattleLine()
    {
        var capital = TestEntityFactory.Capital("bb");
        var screen = TestEntityFactory.Screen("dd");
        var fullScreening = new ScreeningSummary(1, 1);

        Assert.Equal(1.4, HitChanceRules.Modifier(capital, WeaponType.Heavy, fullScreening, false, defines), 6);
        Assert.Equal(1.4, HitChanceRules.Modifier(capital, WeaponType.Light, fullScreening, false, defines), 6);
        Assert.Equal(1.0, HitChanceRules.Modifier(screen, WeaponType.Light, fullScreening, false, defines), 6);
    }

    [Fact]
    public void Modifier_PenalizesMissingOrganizationAndManpower()
    {
        var ship = TestEntityFactory.Screen("dd");
        ship.CurrentOrganization = 0;
        ship.CurrentManpower = ship.MaxManpower / 2;

        var modifier = HitChanceRules.Modifier(ship, WeaponType.Light, new ScreeningSummary(0, 0), false, defines);

        Assert.Equal((1 - 0.5) * (1 - 0.25 * 0.5), modifier, 6);
    }
}
