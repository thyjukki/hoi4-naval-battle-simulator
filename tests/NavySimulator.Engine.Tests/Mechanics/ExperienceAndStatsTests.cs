using NavySimulator.Engine.Battles.Mechanics;
using NavySimulator.Engine.Defines;
using NavySimulator.Engine.Stats;
using NavySimulator.Engine.Tests.TestSupport;

namespace NavySimulator.Engine.Tests.Mechanics;

public class ExperienceAndStatsTests
{
    private readonly GameDefines defines = GameDefines.Vanilla();

    [Fact]
    public void ExperienceForLevel_RoundTripsThroughLevel()
    {
        for (var level = 0; level <= ExperienceRules.MaxLevel(defines); level++)
        {
            var ship = TestEntityFactory.CreateShip("dd", ShipRole.Screen, TestEntityFactory.ScreenStats(), ExperienceRules.ExperienceForLevel(level, defines));
            Assert.Equal(level, ExperienceRules.Level(ship, defines));
        }
    }

    [Fact]
    public void ManpowerLoss_IsFlooredAndCostsExperience()
    {
        var ship = TestEntityFactory.CreateShip("dd", ShipRole.Screen, TestEntityFactory.ScreenStats(), 50);

        var lost = ExperienceRules.ApplyManpowerLoss(ship, 100, 100, defines);

        Assert.Equal(200, lost);
        Assert.Equal(200, ship.CurrentManpower);
        Assert.Equal(50 - 50 * 0.5 * defines.NNavy.EXPERIENCE_LOSS_FACTOR, ship.Experience, 6);
        Assert.Equal(160, ExperienceRules.ApplyManpowerLoss(ship, 100, 100, defines));
        Assert.Equal(40, ship.CurrentManpower);
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
    public void Defines_ValidateRejectsInconsistentPiercingTables()
    {
        var defines = GameDefines.Vanilla();
        Assert.Empty(defines.Validate());

        defines.NNavy.NAVY_PIERCING_THRESHOLDS = [2, 1.5, 1, 0.75, 0.5, 0.25, 0.1, 0.05, 0];
        defines.NNavy.BASE_GUN_COOLDOWNS = [1, 4];

        Assert.Equal(2, defines.Validate().Count);
    }

    [Fact]
    public void Defines_AreIndependentPerInstance()
    {
        var modified = GameDefines.Vanilla();
        modified.NNavy.COMBAT_DAMAGE_TO_STR_FACTOR = 0;

        Assert.Equal(0.6, GameDefines.Vanilla().NNavy.COMBAT_DAMAGE_TO_STR_FACTOR);
    }
}
