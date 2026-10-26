using NavySimulator.Engine.Battles;
using NavySimulator.Engine.Battles.Mechanics;
using NavySimulator.Engine.Defines;
using NavySimulator.Engine.Stats;
using NavySimulator.Engine.Tests.TestSupport;

namespace NavySimulator.Engine.Tests.Mechanics;

public class NavalDominanceRulesTests
{
    private readonly GameDefines defines = GameDefines.Vanilla();

    [Fact]
    public void ShipDominance_IsTheHullFactorTimesTheSpeedRangeHeavyAttackAndCarrierSizeTerms()
    {
        var ship = Ship(100, new ShipStats(Speed: 30, NavalRange: 4000, HeavyAttack: 10, CarrierSize: 2));

        // 1 + (30 - 20) / 40 + (4000 - 2000) / 4000 + 10 × 0.01 + 2 × 0.1
        Assert.Equal(205, NavalDominanceRules.ShipDominance(ship, defines), 9);
    }

    [Fact]
    public void ShipDominance_FallsBelowTheHullFactorForSlowShortRangedShips()
    {
        var ship = Ship(10, new ShipStats(Speed: 10, NavalRange: 1000));

        // 1 - 10 / 40 - 1000 / 4000
        Assert.Equal(5, NavalDominanceRules.ShipDominance(ship, defines), 9);
    }

    [Fact]
    public void ShipDominance_DropsTheTermOfAZeroNeutralValue()
    {
        var ship = Ship(100, new ShipStats(Speed: 30, NavalRange: 4000));
        defines.NNavy.DOMINANCE_PER_SHIP_PER_SPEED_NEUTRAL = 0;
        defines.NNavy.DOMINANCE_PER_SHIP_PER_RANGE_NEUTRAL = 0;

        Assert.Equal(100, NavalDominanceRules.ShipDominance(ship, defines), 9);
    }

    [Fact]
    public void ShipDominance_ReadsTheVariantStats_NotTheSubUnitBonuses()
    {
        var ship = Ship(100, new ShipStats(Speed: 20, NavalRange: 2000, Organization: 30, Hp: 100));
        ship.ApplyExternalModifiers([], new ShipStats(Speed: 0.5));

        Assert.Equal(30, ship.GetFinalStats().Speed, 9);
        Assert.Equal(100, NavalDominanceRules.ShipDominance(ship, defines), 9);

        ship.ApplyExternalModifiers([new ShipStats(Speed: 0.5)], new ShipStats());

        // An equipment bonus is part of the variant: speed 30.
        Assert.Equal(125, NavalDominanceRules.ShipDominance(ship, defines), 9);
    }

    [Fact]
    public void DailyUpdate_GainsAShareOfTheTarget_CappedAtIt_AndLosesOnlyWithoutATarget()
    {
        // S-DOMINANCE-DAILY [RE + LOG] 0x1003c50, vanilla gain 0.02, loss 0.04.
        // Below the target: + trunc(25925.0592 × 0.02) = + 518.50118.
        Assert.Equal((17238 + 518.50118, 25925.0592), NavalDominanceRules.DailyUpdate(17238, 25925.0592, 17238, defines));
        // Ships sunk: the cap takes the value straight down to the new target.
        Assert.Equal((2766.0, 2766.0), NavalDominanceRules.DailyUpdate(17984, 2766, 20228, defines));
        // No task force left: − 4 % of the value the decline starts from, down to 0.
        Assert.Equal((17984 - 809.12, 20228.0), NavalDominanceRules.DailyUpdate(17984, 0, 20228, defines));
        Assert.Equal((0.0, 20228.0), NavalDominanceRules.DailyUpdate(500, 0, 20228, defines));
    }

    [Fact]
    public void SideDominance_LeavesOutSunkShips()
    {
        var first = Ship(100, new ShipStats(Speed: 20, NavalRange: 2000, Hp: 100));
        var second = Ship(50, new ShipStats(Speed: 20, NavalRange: 2000, Hp: 100));

        Assert.Equal(150, NavalDominanceRules.SideDominance([first, second], defines), 9);

        second.CurrentHP = 0;
        Assert.Equal(100, NavalDominanceRules.SideDominance([first, second], defines), 9);
    }

    [Theory]
    [InlineData(10, 5, true)]
    [InlineData(5, 10, false)]
    [InlineData(5, 5, true)]
    [InlineData(0, 0, false)]
    public void IsDominant_WithAtLeastTheEnemysDominance(double own, double enemy, bool expected)
    {
        Assert.Equal(expected, NavalDominanceRules.IsDominant(own, enemy));
    }

    [Fact]
    public void Session_GivesDominanceToTheSideWithMoreOfIt_OnlyWithTheRuleOn()
    {
        Ship[] Attackers() => [Ship(300, TestEntityFactory.CapitalStats(), ShipRole.Capital), Ship(30, TestEntityFactory.ScreenStats())];
        Ship[] Defenders() => [Ship(30, TestEntityFactory.ScreenStats()), Ship(30, TestEntityFactory.ScreenStats())];

        var on = new BattleSession(TestEntityFactory.Scenario(Attackers(), Defenders(), new BattleRules { NavalDominanceEnabled = true }), 1);
        var off = new BattleSession(TestEntityFactory.Scenario(Attackers(), Defenders()), 1);

        Assert.True(on.Attacker.HasNavalDominance);
        Assert.False(on.Defender.HasNavalDominance);
        Assert.True(on.Attacker.NavalDominance > on.Defender.NavalDominance);
        Assert.Contains(on.Notes, note => note.StartsWith("Naval dominance at the start", StringComparison.Ordinal));
        Assert.False(off.Attacker.HasNavalDominance);
        Assert.False(off.Defender.HasNavalDominance);
        Assert.DoesNotContain(off.Notes, note => note.StartsWith("Naval dominance", StringComparison.Ordinal));
    }

    private static Ship Ship(double factor, ShipStats stats, ShipRole role = ShipRole.Screen)
    {
        var hull = new Hull("hull", "archetype", string.Empty, 400, stats, navalDominanceFactor: factor);
        var shipType = new ShipType(role.ToString(), role, [], 0, [hull.Archetype], [], 0);
        return new Ship("ship", new ShipDesign("design", hull, shipType, []));
    }
}
