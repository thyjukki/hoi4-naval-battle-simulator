using NavySimulator.Engine.Battles;
using NavySimulator.Engine.Battles.Mechanics;
using NavySimulator.Engine.Defines;
using NavySimulator.Engine.Tests.TestSupport;

namespace NavySimulator.Engine.Tests.Mechanics;

public class DamageRulesTests
{
    private readonly GameDefines defines = GameDefines.Vanilla();
    private readonly BattleRules noRandomness = new() { DamageRandomness = DamageRandomnessModel.None };

    [Theory]
    [InlineData(10, 4, 0)]
    [InlineData(4, 4, 1)]
    [InlineData(3, 4, 2)]
    [InlineData(2.5, 4, 3)]
    [InlineData(1, 4, 4)]
    [InlineData(0.1, 4, 5)]
    [InlineData(0, 0, 0)]
    public void PiercingTier_UsesVanillaThresholdsAndTreatsZeroArmorAsFullyPierced(double piercing, double armor, int expectedTier)
    {
        Assert.Equal(expectedTier, DamageRules.PiercingTier(piercing, armor, defines));
    }

    [Theory]
    [InlineData(0.0, 0.975)]
    [InlineData(0.99999, 1.474995)]
    public void RandomFactor_IsOnePlusRTimesUMinusFivePercent(double roll, double expected)
    {
        Assert.Equal(expected, DamageRules.RandomFactor(DamageRandomnessModel.Game, 0.5, new ScriptedRandomSource(roll)), 6);
    }

    [Fact]
    public void Profile_GunCriticalMultiplierIsOnePlusFiveTimesMissingReliability()
    {
        var profile = DamageRules.Profile(TestEntityFactory.Screen("dd"), TestEntityFactory.Screen("target"), WeaponSlot.Light, 1.0, defines);

        // Reliability 0.8; piercing 5 vs armor 2 is tier 0 (critical value 2).
        Assert.Equal(2.0, profile.CriticalMultiplier, 9);
        Assert.Equal(0.05 * 0.2 * 2, profile.CriticalChance, 9);
    }

    [Fact]
    public void Profile_DepthChargesGoThroughThePiercingTiers()
    {
        var hunter = TestEntityFactory.CreateShip("dd", ShipRole.Screen, TestEntityFactory.ScreenStats() with { DepthChargeAttack = 10, LightPiercing = 0.5 });
        var submarine = TestEntityFactory.CreateShip("ss", ShipRole.Submarine, TestEntityFactory.SubmarineStats() with { Armor = 5 });

        var profile = DamageRules.Profile(hunter, submarine, WeaponSlot.Light, 1.0, defines);

        // Light piercing 0.5 vs armor 5 is ratio 0.1: tier 4, damage factor 0.3; depth charge damage x0.7.
        Assert.True(profile.IsDepthCharge);
        Assert.Equal(4, profile.PiercingTier);
        Assert.Equal(10 * 0.7 * 0.3, profile.BaseDamage, 9);
    }

    [Fact]
    public void Profile_TorpedoesIgnoreArmorAndUseTheTargetsReduction()
    {
        var shooter = TestEntityFactory.Screen("dd", torpedoAttack: 10);
        var target = TestEntityFactory.CreateShip("bb", ShipRole.Capital, TestEntityFactory.CapitalStats() with { TorpedoDamageReductionFactor = 0.25 });

        var profile = DamageRules.Profile(shooter, target, WeaponSlot.Torpedo, 1.0, defines);

        Assert.Equal(10, profile.BaseDamage, 9);
        Assert.Equal(0.75, profile.TorpedoFactor);
        Assert.Equal(2.0, profile.CriticalMultiplier);
    }

    [Fact]
    public void DamageMultiplier_AddsPositioningPenaltyAndExperienceModifiers()
    {
        var veteran = TestEntityFactory.CreateShip("vet", ShipRole.Screen, TestEntityFactory.ScreenStats(), experience: 95);
        var green = TestEntityFactory.Screen("green");

        // Shooter level 4: naval_damage_factor +0.3; target level 0: naval_defense_factor -0.1.
        Assert.Equal(1.4, DamageRules.DamageMultiplier(veteran, green, 1.0, defines), 9);
        Assert.Equal(1.4 - 0.5 * 0.5, DamageRules.DamageMultiplier(veteran, green, 0.5, defines), 9);
    }

    [Fact]
    public void RollHit_MatchesWikiOrganizationExample()
    {
        // A 100 HP / 50 org ship hit for 50 loses 30 HP each time and 0, 15, 30 organization.
        var target = TestEntityFactory.CreateShip("target", ShipRole.Screen, TestEntityFactory.ScreenStats() with { Hp = 100, Organization = 50 });
        var profile = new ShotProfile(50, 0, 1, 0, 1, null, false);
        var organization = new List<double>();

        for (var i = 0; i < 3; i++)
        {
            var hit = DamageRules.RollHit(target, profile, defines, noRandomness, new ScriptedRandomSource(0.5));
            var applied = DamageRules.Apply(target, hit.StrDamage, hit.OrgDamage, defines);
            Assert.Equal(30, applied.HpDamage, 9);
            organization.Add(Math.Round(applied.OrganizationDamage, 9));
        }

        Assert.Equal([0.0, 15.0, 30.0], organization);
    }

    [Fact]
    public void RollHit_CriticalMultipliesHpAndOrganizationDamage()
    {
        var target = TestEntityFactory.CreateShip("target", ShipRole.Screen, TestEntityFactory.ScreenStats() with { Hp = 100, Organization = 50 });
        target.CurrentHP = 50;
        var profile = new ShotProfile(10, 0, 1, 1.0, 2.0, null, false);

        var hit = DamageRules.RollHit(target, profile, defines, noRandomness, new ScriptedRandomSource(0.0));

        Assert.True(hit.IsCritical);
        Assert.Equal(10 * 0.6 * 2, hit.StrDamage, 9);
        Assert.Equal(10 * 0.5 * 2, hit.OrgDamage, 9);
    }

    [Fact]
    public void Apply_ExperienceKeepsTheShareOfHpLeftAndManpowerFollowsHpDamage()
    {
        var target = TestEntityFactory.CreateShip("target", ShipRole.Screen, TestEntityFactory.ScreenStats() with { Hp = 100 }, experience: 50);

        DamageRules.Apply(target, 30, 0, defines);

        Assert.Equal(50 * 0.7, target.Experience, 9);
        Assert.Equal(400 - 60, target.CurrentManpower);
    }

    [Fact]
    public void Apply_ManpowerNeverDropsBelowTheFloor()
    {
        var target = TestEntityFactory.CreateShip("target", ShipRole.Screen, TestEntityFactory.ScreenStats() with { Hp = 100 });

        // Each hit for the full max HP would cost half the crew; the second is capped at the 10% floor.
        DamageRules.Apply(target, 100, 0, defines);
        Assert.Equal(200, target.CurrentManpower);
        DamageRules.Apply(target, 100, 0, defines);
        Assert.Equal(40, target.CurrentManpower);
    }

    [Fact]
    public void Apply_ReportsSinkingOnlyOnce()
    {
        var ship = TestEntityFactory.Screen("target");

        var first = DamageRules.Apply(ship, 1000, 0, defines);
        var second = DamageRules.Apply(ship, 1000, 0, defines);

        Assert.True(first.Sunk);
        Assert.False(second.Sunk);
    }
}
