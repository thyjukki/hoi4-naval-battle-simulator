using NavySimulator.Engine.Battles;
using NavySimulator.Engine.Battles.Mechanics;
using NavySimulator.Engine.Defines;
using NavySimulator.Engine.Tests.TestSupport;

namespace NavySimulator.Engine.Tests.Mechanics;

public class DamageRulesTests
{
    private readonly GameDefines defines = GameDefines.Vanilla();

    [Theory]
    [InlineData(10, 4, 0)]
    [InlineData(4, 4, 1)]
    [InlineData(3, 4, 2)]
    [InlineData(2.5, 4, 3)]
    [InlineData(1, 4, 4)]
    [InlineData(0.1, 4, 5)]
    [InlineData(1, 0, 0)]
    public void PiercingTier_UsesVanillaThresholds(double piercing, double armor, int expectedTier)
    {
        Assert.Equal(expectedTier, DamageRules.PiercingTier(piercing, armor, defines));
    }

    [Theory]
    [InlineData(DamageRandomnessModel.WikiDecoded, 0.0, 0.975)]
    [InlineData(DamageRandomnessModel.WikiDecoded, 0.999999, 1.475)]
    [InlineData(DamageRandomnessModel.DefineComment, 0.999999, 0.5)]
    [InlineData(DamageRandomnessModel.DefineComment, 0.0, 1.0)]
    [InlineData(DamageRandomnessModel.None, 0.5, 1.0)]
    public void RandomFactor_CoversDocumentedRanges(DamageRandomnessModel model, double roll, double expected)
    {
        var factor = DamageRules.RandomFactor(model, 0.5, new ScriptedRandomSource(roll));

        Assert.Equal(expected, factor, 4);
    }

    [Fact]
    public void Apply_MatchesWikiOrganizationExample()
    {
        // Wiki: a 100 HP / 50 org ship hit for 50 loses 30 HP per hit, and 0, 15, 30 org on the first three hits.
        var ship = TestEntityFactory.CreateShip("target", ShipRole.Screen, TestEntityFactory.ScreenStats() with { Hp = 100, Organization = 50 });
        var navy = defines.NNavy;

        var results = Enumerable.Range(0, 3)
            .Select(_ => DamageRules.Apply(ship, 50, navy.COMBAT_DAMAGE_TO_STR_FACTOR, navy.COMBAT_DAMAGE_TO_ORG_FACTOR, defines))
            .ToList();

        Assert.All(results, result => Assert.Equal(30, result.HpDamage, 6));
        Assert.Equal([0.0, 15.0, 30.0], results.Select(result => Math.Round(result.OrganizationDamage, 6)));
        Assert.Equal(10, ship.CurrentHP, 6);
    }

    [Fact]
    public void Apply_ReportsSinkingOnlyOnce()
    {
        var ship = TestEntityFactory.Screen("target");

        var first = DamageRules.Apply(ship, 1000, 0.6, 1, defines);
        var second = DamageRules.Apply(ship, 1000, 0.6, 1, defines);

        Assert.True(first.Sunk);
        Assert.False(second.Sunk);
        Assert.True(ship.IsSunk);
    }

    [Fact]
    public void Calculate_TorpedoesIgnoreArmor()
    {
        var shooter = TestEntityFactory.Screen("shooter", torpedoAttack: 10);
        var armored = TestEntityFactory.Capital("bb");
        var rules = new BattleRules { DamageRandomness = DamageRandomnessModel.None };

        // Roll 0.99 avoids a critical hit.
        var damage = DamageRules.Calculate(shooter, armored, WeaponType.Torpedo, 10, 0, 1.0, defines, rules, new ScriptedRandomSource(0.99));

        Assert.Equal(1.0, damage.PiercingDamageFactor);
        Assert.False(damage.IsCritical);
        Assert.Equal(10 * ExperienceRules.DamageFactor(shooter, defines), damage.Damage, 6);
    }
}
