using NavySimulator.Engine.Battles;
using NavySimulator.Engine.Battles.Mechanics;
using NavySimulator.Engine.Defines;
using NavySimulator.Engine.Tests.TestSupport;

namespace NavySimulator.Engine.Tests.Mechanics;

public class TargetingRulesTests
{
    private readonly GameDefines defines = GameDefines.Vanilla();

    private static Ship Carrier(string id)
    {
        return TestEntityFactory.CreateShip(id, ShipRole.Carrier, TestEntityFactory.CapitalStats());
    }

    [Fact]
    public void LightGuns_TargetClosestGroup()
    {
        var enemy = TestEntityFactory.Side("enemy", TestEntityFactory.Screen("dd"), TestEntityFactory.Capital("bb"));

        var groups = TargetingRules.ReachableGroups(TestEntityFactory.Screen("shooter"), WeaponType.Light, enemy, SubmarineEngagementRisk.Medium, new ScriptedRandomSource(0.5));

        Assert.Equal([GroupType.Screen], groups);
    }

    [Fact]
    public void LightGuns_MoveToNextGroupWhenClosestGroupSankEarlierThisHour()
    {
        var screen = TestEntityFactory.Screen("dd");
        var enemy = TestEntityFactory.Side("enemy", screen, TestEntityFactory.Capital("bb"));
        screen.CurrentHP = 0;

        var groups = TargetingRules.ReachableGroups(TestEntityFactory.Screen("shooter"), WeaponType.Light, enemy, SubmarineEngagementRisk.Medium, new ScriptedRandomSource(0.5));

        Assert.Equal([GroupType.Capital], groups);
    }

    [Fact]
    public void LightGuns_FromCarriersNeverTargetCarrierGroup()
    {
        var enemy = TestEntityFactory.Side("enemy", Carrier("cv"));

        var groups = TargetingRules.ReachableGroups(Carrier("shooter"), WeaponType.Light, enemy, SubmarineEngagementRisk.Medium, new ScriptedRandomSource(0.5));

        Assert.Empty(groups);
    }

    [Fact]
    public void HeavyGuns_ReachFirstTwoGroups()
    {
        var enemy = TestEntityFactory.Side("enemy", TestEntityFactory.Screen("dd"), TestEntityFactory.Capital("bb"), Carrier("cv"));

        var groups = TargetingRules.ReachableGroups(TestEntityFactory.Capital("shooter"), WeaponType.Heavy, enemy, SubmarineEngagementRisk.Medium, new ScriptedRandomSource(0.5));

        Assert.Equal([GroupType.Screen, GroupType.Capital], groups);
    }

    [Fact]
    public void Torpedoes_BypassScreensOnlyWithMissingScreeningEfficiency()
    {
        var enemy = TestEntityFactory.Side("enemy", TestEntityFactory.Screen("dd"), TestEntityFactory.Capital("bb"), Carrier("cv"));
        var shooter = TestEntityFactory.Screen("shooter", torpedoAttack: 5);

        enemy.Screening = new ScreeningSummary(1, 1);
        var screened = TargetingRules.ReachableGroups(shooter, WeaponType.Torpedo, enemy, SubmarineEngagementRisk.Medium, new ScriptedRandomSource(0.0));

        enemy.Screening = new ScreeningSummary(0, 0);
        var unscreened = TargetingRules.ReachableGroups(shooter, WeaponType.Torpedo, enemy, SubmarineEngagementRisk.Medium, new ScriptedRandomSource(0.0));

        Assert.Equal([GroupType.Screen], screened);
        Assert.Equal([GroupType.Screen, GroupType.Capital, GroupType.Carrier], unscreened);
    }

    [Fact]
    public void TargetWeight_AppliesRetreatPenaltyOnceAndActiveBonus()
    {
        var shooter = TestEntityFactory.Capital("bb");
        var target = TestEntityFactory.Screen("dd");
        target.CurrentStatus = ShipStatus.Retreating;

        Assert.Equal(3 * 0.5, TargetingRules.TargetWeight(shooter, WeaponType.Heavy, target, false, defines), 6);
        Assert.Equal(3 * 0.5 * 1.5, TargetingRules.TargetWeight(shooter, WeaponType.Heavy, target, true, defines), 6);
    }

    [Fact]
    public void TargetWeight_DoublesForFullyDamagedTargets()
    {
        var shooter = TestEntityFactory.Screen("shooter");
        var target = TestEntityFactory.Screen("dd");
        target.CurrentHP = 0.001;

        Assert.Equal(6 * 2, TargetingRules.TargetWeight(shooter, WeaponType.Light, target, false, defines), 3);
    }
}
