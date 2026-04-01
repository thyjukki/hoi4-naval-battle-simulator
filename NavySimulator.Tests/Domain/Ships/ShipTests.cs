using NavySimulator.Domain;
using NavySimulator.Domain.Stats;
using NavySimulator.Tests.TestSupport;
using Xunit;

namespace NavySimulator.Tests.Domain.Ships;

public class ShipTests
{
    [Fact]
    public void GetFinalStats_UsesBaseStatsAndExperienceAttackMultiplier()
    {
        var baseStats = new ShipStats(
            Speed: 30,
            Organization: 60,
            Hp: 200,
            LightAttack: 10,
            HeavyAttack: 20,
            TorpedoAttack: 30,
            DepthChargeAttack: 40,
            SurfaceVisibility: 25,
            SubVisibility: 8,
            ProductionCost: 1000);
        var ship = TestEntityFactory.CreateShip("ship_001", "test_design", ShipRole.Screen, 7, baseStats);

        var finalStats = ship.GetFinalStats();
        var expectedAttackMultiplier = 1.0 + Hoi4Defines.ShipExperienceBonusMaxNavalDamageFactor;

        Assert.Equal(baseStats.Speed, finalStats.Speed);
        Assert.Equal(baseStats.Organization, finalStats.Organization);
        Assert.Equal(baseStats.Hp, finalStats.Hp);
        Assert.Equal(baseStats.SurfaceVisibility, finalStats.SurfaceVisibility);

        Assert.Equal(baseStats.LightAttack * expectedAttackMultiplier, finalStats.LightAttack, 6);
        Assert.Equal(baseStats.HeavyAttack * expectedAttackMultiplier, finalStats.HeavyAttack, 6);
        Assert.Equal(baseStats.TorpedoAttack * expectedAttackMultiplier, finalStats.TorpedoAttack, 6);
        Assert.Equal(baseStats.DepthChargeAttack * expectedAttackMultiplier, finalStats.DepthChargeAttack, 6);
    }

    [Fact]
    public void ApplyExternalModifiers_UpdatesEffectiveStatsAndCurrentState()
    {
        var baseStats = new ShipStats(
            Organization: 50,
            Hp: 100,
            LightAttack: 20,
            HeavyAttack: 10,
            TorpedoAttack: 5,
            DepthChargeAttack: 2,
            ProductionCost: 500);
        var ship = TestEntityFactory.CreateShip("ship_001", "test_design", ShipRole.Screen, 2, baseStats);

        var modifiers = new ShipStats(Hp: 10, Organization: 5, LightAttack: 3, HeavyAttack: 2, TorpedoAttack: 1, DepthChargeAttack: 1);
        var averages = new ShipStats(Hp: 5, Organization: 5, LightAttack: 2, HeavyAttack: 1, TorpedoAttack: 1, DepthChargeAttack: 0.5);
        var multipliers = new ShipStats(Hp: 0.1, Organization: 0.2, LightAttack: 0.5, HeavyAttack: 0.25, TorpedoAttack: 0.1, DepthChargeAttack: 0.5);

        ship.ApplyExternalModifiers(modifiers, averages, multipliers);

        var expectedEffectiveHp = (baseStats.Hp + modifiers.Hp + averages.Hp) * (1 + multipliers.Hp);
        var expectedEffectiveOrganization = (baseStats.Organization + modifiers.Organization + averages.Organization) * (1 + multipliers.Organization);
        var expectedEffectiveLightAttack = (baseStats.LightAttack + modifiers.LightAttack + averages.LightAttack) * (1 + multipliers.LightAttack);

        var step = (Hoi4Defines.ShipExperienceBonusMaxNavalDamageFactor - Hoi4Defines.ShipExperienceBonusMinNavalDamageFactor) / Hoi4Defines.UNIT_EXP_LEVELS.Length;
        var expectedModifier = Hoi4Defines.ShipExperienceBonusMinNavalDamageFactor + step * ship.ExperienceLevel;
        var expectedAttackMultiplier = 1.0 + expectedModifier;

        Assert.Equal(expectedEffectiveHp, ship.CurrentHP, 6);
        Assert.Equal(expectedEffectiveOrganization, ship.CurrentOrganization, 6);

        var finalStats = ship.GetFinalStats();
        Assert.Equal(expectedEffectiveHp, finalStats.Hp, 6);
        Assert.Equal(expectedEffectiveOrganization, finalStats.Organization, 6);
        Assert.Equal(expectedEffectiveLightAttack * expectedAttackMultiplier, finalStats.LightAttack, 6);
    }

    [Fact]
    public void ExperienceLevel_MapsFromXpThresholdsAndClamps()
    {
        var ship = TestEntityFactory.CreateShip("ship_001", "test_design", ShipRole.Screen, 2, new ShipStats(Hp: 100, Organization: 50));

        var expectedLevel2Xp = Hoi4Defines.UNIT_EXP_LEVELS[1] * Hoi4Defines.NAVY_MAX_XP;
        Assert.Equal(expectedLevel2Xp, ship.Experience, 6);
        Assert.Equal(2, ship.ExperienceLevel);

        ship.Experience = -10;
        Assert.Equal(0, ship.Experience, 6);
        Assert.Equal(0, ship.ExperienceLevel);
        Assert.Equal(Hoi4Defines.ShipExperienceBonusMinNavalDamageFactor, ship.GetShipExperienceAttackModifier(), 6);

        ship.Experience = Hoi4Defines.NAVY_MAX_XP + 999;
        Assert.Equal(Hoi4Defines.NAVY_MAX_XP, ship.Experience, 6);
        Assert.Equal(Hoi4Defines.UNIT_EXP_LEVELS.Length, ship.ExperienceLevel);
        Assert.Equal(Hoi4Defines.ShipExperienceBonusMaxNavalDamageFactor, ship.GetShipExperienceAttackModifier(), 6);
    }

    [Fact]
    public void ApplyDamage_TracksManpowerLossWithFloor_AndDailyRecovery()
    {
        var originalStrFactor = Hoi4Defines.COMBAT_DAMAGE_TO_STR_FACTOR;

        var ship = TestEntityFactory.CreateShip(
            "ship_001",
            "test_design",
            ShipRole.Screen,
            2,
            new ShipStats(Hp: 100, Organization: 50));

        var maxHp = ship.GetFinalStats().Hp;

        // Repeated full-strength hits simulate repeated STR loss events and validate manpower floor.
        for (var i = 0; i < 10; i++)
        {
            ship.CurrentHP = maxHp;
            ship.ApplyDamage(400);
        }

        var expectedMinManpower = ship.MaxManpower * Hoi4Defines.MIN_MANPOWER_RATIO_TO_DROP;
        Assert.Equal(expectedMinManpower, ship.CurrentManpower, 6);

        ship.ApplyDailyManpowerRecovery();
        var expectedAfterRecovery = expectedMinManpower + ship.MaxManpower * Hoi4Defines.DAILY_MANPOWER_GAIN_RATIO;
        Assert.Equal(expectedAfterRecovery, ship.CurrentManpower, 6);
    }

    [Fact]
    public void ApplyDamage_WhenManpowerDrops_AlsoReducesExperience()
    {
        var ship = TestEntityFactory.CreateShip(
            "ship_001",
            "test_design",
            ShipRole.Carrier,
            4,
            new ShipStats(Hp: 325, Organization: 40),
            7000);

        ship.Experience = 40;

        ship.ApplyDamage(18.33462*2);

        Assert.Equal(306.66538, ship.CurrentHP, 2);
        Assert.Equal(40.00, ship.CurrentOrganization, 2);
        Assert.Equal(6803, ship.CurrentManpower);
        Assert.Equal(37.75, ship.Experience, 2);
        
        ship.ApplyDamage(18.0063*2);
        ship.ApplyDailyManpowerRecovery();
        
        Assert.Equal(288.65908, ship.CurrentHP, 2);
        Assert.Equal(37.96817, ship.CurrentOrganization, 2);
        Assert.Equal(6959, ship.CurrentManpower);
        Assert.Equal(35.60, ship.Experience, 2);
        
        ship.ApplyDamage(18.99159*2);
        
        Assert.Equal(269.66749, ship.CurrentHP, 2);
        Assert.Equal(33.7209, ship.CurrentOrganization, 2);
        Assert.Equal(6754, ship.CurrentManpower);
        Assert.Equal(33.5, ship.Experience, 2);
    }

}

