using NavySimulator.Engine.Battles.Mechanics;
using NavySimulator.Engine.Defines;
using NavySimulator.Engine.Stats;
using NavySimulator.Engine.Tests.TestSupport;

namespace NavySimulator.Engine.Tests.Mechanics;

public class ExperienceAndStatsTests
{
    private readonly GameDefines defines = GameDefines.Vanilla();

    [Fact]
    public void ExperienceForLevel_RoundTripsThroughTheFiveVanillaLevels()
    {
        Assert.Equal(4, ExperienceRules.MaxLevel(defines));

        for (var level = 0; level <= ExperienceRules.MaxLevel(defines); level++)
        {
            var ship = TestEntityFactory.CreateShip("dd", ShipRole.Screen, TestEntityFactory.ScreenStats(), ExperienceRules.ExperienceForLevel(level, defines));
            Assert.Equal(level, ExperienceRules.Level(ship, defines));
        }
    }

    [Theory]
    [InlineData(0, -0.1, -0.1)]
    [InlineData(1, 0.0, 0.0)]
    [InlineData(2, 0.1, 0.2 / 3)]
    [InlineData(4, 0.3, 0.2)]
    public void LevelModifiers_ComeFromTheStaticExperienceBlocks(int level, double damage, double defense)
    {
        var ship = TestEntityFactory.CreateShip("dd", ShipRole.Screen, TestEntityFactory.ScreenStats(), ExperienceRules.ExperienceForLevel(level, defines));

        Assert.Equal(damage, ExperienceRules.NavalDamageFactor(ship, defines), 9);
        Assert.Equal(defense, ExperienceRules.NavalDefenseFactor(ship, defines), 9);
    }

    [Fact]
    public void HourlyGain_UsesGroupSizesPlusOne()
    {
        Assert.Equal(10 * 1 * (10.0 / 10.0) * 0.04, ExperienceRules.HourlyGain(9, 9, false, defines), 9);
        Assert.Equal(10 * 1 * (21.0 / 6.0) * 0.04, ExperienceRules.HourlyGain(20, 5, false, defines), 9);
        Assert.Equal(10 * 1 * 1 * 0.04 * 0.04, ExperienceRules.HourlyGain(9, 9, true, defines), 9);
    }

    [Fact]
    public void ShipStats_KeepsTorpedoAndDepthChargeHitFactorsThroughAddAndScale()
    {
        var stats = new ShipStats(TorpedoHitChanceFactor: 0.1, DepthChargeHitChanceFactor: 0.2)
            .Add(new ShipStats(TorpedoHitChanceFactor: 0.05))
            .Scale(new ShipStats());

        Assert.Equal(0.15, stats.TorpedoHitChanceFactor, 6);
        Assert.Equal(0.2, stats.DepthChargeHitChanceFactor, 6);
    }

    [Fact]
    public void Defines_AreIndependentPerInstance()
    {
        var modified = GameDefines.Vanilla();
        modified.NNavy.COMBAT_DAMAGE_TO_STR_FACTOR = 0;
        modified.StaticModifiers.Blocks[StaticModifiers.ScreeningBonus][StaticModifiers.NavalHitChance] = 0;

        Assert.Equal(0.6, GameDefines.Vanilla().NNavy.COMBAT_DAMAGE_TO_STR_FACTOR);
        Assert.Equal(0.4, GameDefines.Vanilla().StaticModifiers.Get(StaticModifiers.ScreeningBonus, StaticModifiers.NavalHitChance));
    }

    [Fact]
    public void Defines_ValidateRejectsInconsistentPiercingTables()
    {
        var defines = GameDefines.Vanilla();
        Assert.Empty(defines.Validate());

        defines.NNavy.NAVY_PIERCING_THRESHOLDS = [2, 1.5, 1, 0.75, 0.5, 0.25, 0.1, 0.05, 0];
        defines.NNavy.BASE_GUN_COOLDOWNS = [1, 4];

        Assert.Equal(2, defines.Validate().Count);
    }
}
