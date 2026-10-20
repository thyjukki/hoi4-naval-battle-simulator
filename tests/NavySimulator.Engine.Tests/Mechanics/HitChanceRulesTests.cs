using NavySimulator.Engine.Battles;
using NavySimulator.Engine.Battles.Mechanics;
using NavySimulator.Engine.Defines;
using NavySimulator.Engine.Environment;
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
        var result = HitChanceRules.Calculate(TestEntityFactory.Screen("dd"), LightCruiser1936(), WeaponSlot.Light, FullScreening, BattleEnvironment.None, defines);

        Assert.Equal(45, result.WeaponHitProfile);
        Assert.Equal(Math.Pow(41.6667 / 45, 2), result.ProfileFactor, 3);
        Assert.Equal(0.1 * result.ProfileFactor, result.Final, 6);
    }

    [Fact]
    public void Calculate_AppliesTheMinimumLast()
    {
        var tiny = TestEntityFactory.CreateShip("tiny", ShipRole.Screen, TestEntityFactory.ScreenStats() with { SurfaceVisibility = 0.5 });

        var result = HitChanceRules.Calculate(TestEntityFactory.Capital("bb"), tiny, WeaponSlot.Heavy, FullScreening, BattleEnvironment.None, defines);

        Assert.Equal(0.005, result.Final, 9);
    }

    [Fact]
    public void Calculate_HasNoUpperCap()
    {
        var sharpshooter = TestEntityFactory.CreateShip("dd", ShipRole.Screen, TestEntityFactory.ScreenStats() with { LightHitChanceFactor = 20 });

        var result = HitChanceRules.Calculate(sharpshooter, LightCruiser1936(), WeaponSlot.Light, FullScreening, BattleEnvironment.None, defines);

        Assert.True(result.Final > 1);
    }

    [Fact]
    public void ModifierSum_AddsScreeningAndNightInsteadOfMultiplying()
    {
        var capital = TestEntityFactory.Capital("bb");

        Assert.Equal(1.4, HitChanceRules.ModifierSum(capital, WeaponSlot.Heavy, FullScreening, BattleEnvironment.None, defines), 9);
        Assert.Equal(1.15, HitChanceRules.ModifierSum(capital, WeaponSlot.Heavy, FullScreening, new BattleEnvironment(1), defines), 9);
        Assert.Equal(1.0, HitChanceRules.ModifierSum(TestEntityFactory.Screen("dd"), WeaponSlot.Light, FullScreening, BattleEnvironment.None, defines), 9);
    }

    [Fact]
    public void ModifierSum_AddsTheScreeningBonusWeaponFactors_AsLoggedInTheGame()
    {
        // TFB's screening_bonus; the capital designs of jukki_usa_vs_bc_japan have heavy and light hit factors 0.15.
        // The game logged modifiers 1.9857 (heavy) and 1.5356 (light) at screening 0.64285, 2.4130 and 1.7328 at 0.97144.
        var tfb = GameDefines.Vanilla();
        tfb.StaticModifiers.Blocks[StaticModifiers.ScreeningBonus] = new(StringComparer.Ordinal)
        {
            [StaticModifiers.NavalHitChance] = 0.3,
            [StaticModifiers.NavalHeavyGunHitChanceFactor] = 1,
            [StaticModifiers.NavalLightGunHitChanceFactor] = 0.3
        };
        var capital = TestEntityFactory.CreateShip("bb", ShipRole.Capital,
            TestEntityFactory.CapitalStats() with { HeavyHitChanceFactor = 0.15, LightHitChanceFactor = 0.15 });
        var screen = TestEntityFactory.CreateShip("dd", ShipRole.Screen, TestEntityFactory.ScreenStats() with { LightHitChanceFactor = 0.15 });

        Assert.Equal(1.9857, HitChanceRules.ModifierSum(capital, WeaponSlot.Heavy, new(0.64285, 1), BattleEnvironment.None, tfb), 3);
        Assert.Equal(1.5356, HitChanceRules.ModifierSum(capital, WeaponSlot.Light, new(0.64285, 1), BattleEnvironment.None, tfb), 3);
        Assert.Equal(2.4130, HitChanceRules.ModifierSum(capital, WeaponSlot.Heavy, new(0.97144, 1), BattleEnvironment.None, tfb), 3);
        Assert.Equal(1.7328, HitChanceRules.ModifierSum(capital, WeaponSlot.Light, new(0.97144, 1), BattleEnvironment.None, tfb), 3);
        Assert.Equal(1.15, HitChanceRules.ModifierSum(screen, WeaponSlot.Light, new(0.97144, 1), BattleEnvironment.None, tfb), 9);
    }

    [Fact]
    public void ModifierSum_AddsCountryHitChanceModifiers()
    {
        var screen = TestEntityFactory.Screen("dd");
        screen.CountryModifiers = new([
            new(StaticModifiers.NavalHitChance, 0.1),
            new(StaticModifiers.NavalTorpedoHitChanceFactor, 0.05)
        ]);

        Assert.Equal(1.1, HitChanceRules.ModifierSum(screen, WeaponSlot.Light, FullScreening, BattleEnvironment.None, defines), 9);
        Assert.Equal(1.15, HitChanceRules.ModifierSum(screen, WeaponSlot.Torpedo, FullScreening, BattleEnvironment.None, defines), 9);
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

        var result = HitChanceRules.Calculate(TestEntityFactory.Screen("dd"), submarine, WeaponSlot.Light, FullScreening, BattleEnvironment.None, defines);

        Assert.Equal(100, result.WeaponHitProfile);
        Assert.Equal(0.1 * 1.1, result.BaseChance, 9);
        Assert.Equal(1000 / 27.5, result.ShipHitProfile, 6);
    }
}
