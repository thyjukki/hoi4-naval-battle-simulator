using NavySimulator.Engine.Battles;
using NavySimulator.Engine.Battles.Mechanics;
using NavySimulator.Engine.Defines;
using NavySimulator.Engine.Tests.TestSupport;

namespace NavySimulator.Engine.Tests.Mechanics;

public class TargetingRulesTests
{
    private readonly GameDefines defines = GameDefines.Vanilla();

    [Fact]
    public void MaxLine_LightReachesTheClosestNonEmptyLine()
    {
        var anyRoll = new ScriptedRandomSource(0.5);
        var full = new ScreeningSummary(1, 1);

        Assert.Equal(1, TargetingRules.MaxLine(WeaponSlot.Light, new EnemyLineCounts(1, 1, 1, 0, 0), full, anyRoll));
        Assert.Equal(2, TargetingRules.MaxLine(WeaponSlot.Light, new EnemyLineCounts(0, 1, 1, 0, 0), full, anyRoll));
        Assert.Equal(3, TargetingRules.MaxLine(WeaponSlot.Light, new EnemyLineCounts(0, 0, 1, 0, 0), full, anyRoll));
    }

    [Fact]
    public void MaxLine_HeavyReachesTheFirstTwoNonEmptyLines()
    {
        var anyRoll = new ScriptedRandomSource(0.5);
        var full = new ScreeningSummary(1, 1);

        Assert.Equal(2, TargetingRules.MaxLine(WeaponSlot.Heavy, new EnemyLineCounts(1, 1, 1, 0, 0), full, anyRoll));
        Assert.Equal(3, TargetingRules.MaxLine(WeaponSlot.Heavy, new EnemyLineCounts(0, 1, 1, 0, 0), full, anyRoll));
        Assert.Equal(3, TargetingRules.MaxLine(WeaponSlot.Heavy, new EnemyLineCounts(1, 0, 1, 0, 0), full, anyRoll));
    }

    [Theory]
    [InlineData(0.40, 1)]
    [InlineData(0.50, 1)]
    [InlineData(0.60, 3)]
    public void MaxLine_TorpedoesUseOneRollAgainstBothScreeningValues(double roll, int expected)
    {
        var screening = new ScreeningSummary(0.5, 0.3);

        Assert.Equal(expected, TargetingRules.MaxLine(WeaponSlot.Torpedo, new EnemyLineCounts(1, 1, 1, 0, 0), screening, new ScriptedRandomSource(roll)));
    }

    [Fact]
    public void MinLine_SubmarinesKeepToTheCarrierLineOnLowEngagementUntilItIsEmpty()
    {
        var submarine = TestEntityFactory.Submarine("ss");

        Assert.Equal(3, TargetingRules.MinLine(submarine, Aggressiveness.EngageAtLowRisk, new EnemyLineCounts(1, 1, 0, 2, 0)));
        Assert.Equal(2, TargetingRules.MinLine(submarine, Aggressiveness.EngageAtLowRisk, new EnemyLineCounts(1, 1, 0, 0, 0)));
        Assert.Equal(2, TargetingRules.MinLine(submarine, Aggressiveness.EngageAtMediumRisk, new EnemyLineCounts(1, 1, 0, 0, 0)));
        Assert.Equal(1, TargetingRules.MinLine(submarine, Aggressiveness.EngageAtMediumRisk, new EnemyLineCounts(1, 0, 0, 0, 0)));
        Assert.Equal(1, TargetingRules.MinLine(submarine, Aggressiveness.EngageAtHighRisk, new EnemyLineCounts(1, 1, 1, 0, 0)));
        Assert.Equal(1, TargetingRules.MinLine(TestEntityFactory.Screen("dd"), Aggressiveness.EngageAtLowRisk, new EnemyLineCounts(1, 1, 1, 0, 0)));
    }

    [Fact]
    public void TargetWeight_MatchesTheDecodedTable()
    {
        var gunship = TestEntityFactory.Capital("bb");
        var capital = TestEntityFactory.Capital("target_bb");
        var screen = TestEntityFactory.Screen("target_dd");
        var carrier = TestEntityFactory.Carrier("target_cv");

        Assert.Equal(30, TargetingRules.TargetWeight(gunship, WeaponSlot.Heavy, capital, defines), 9);
        Assert.Equal(2, TargetingRules.TargetWeight(gunship, WeaponSlot.Light, capital, defines), 9);
        Assert.Equal(3, TargetingRules.TargetWeight(gunship, WeaponSlot.Heavy, screen, defines), 9);
        Assert.Equal(6, TargetingRules.TargetWeight(gunship, WeaponSlot.Light, screen, defines), 9);
        Assert.Equal(15, TargetingRules.TargetWeight(gunship, WeaponSlot.Torpedo, carrier, defines), 9);
        Assert.Equal(1, TargetingRules.TargetWeight(gunship, WeaponSlot.Light, carrier, defines), 9);
    }

    [Fact]
    public void TargetWeight_IsTakenInTheGamesFixedPoint()
    {
        // HP ratio 1/3 rounds to 0.33333, so 6 × (2 − 0.33333) × 1.5 = 15.00003 rather than 15.
        var shooter = TestEntityFactory.Screen("dd");
        var target = TestEntityFactory.Screen("target");
        target.CurrentHP = 100.0 / 3;
        target.CurrentStatus = ShipStatus.Active;

        Assert.Equal(15.00003, TargetingRules.TargetWeight(shooter, WeaponSlot.Light, target, defines), 9);
    }

    [Theory]
    [InlineData(0.49999, "a")]
    [InlineData(0.5, "a")]
    [InlineData(0.50001, "b")]
    public void WeightedPick_ComparesTheTruncatedThresholdInFixedPoint(double roll, string expected)
    {
        // T = trunc(U × 2) in fx5; "a" takes [0, 1].
        var a = TestEntityFactory.Screen("a");
        var b = TestEntityFactory.Screen("b");

        Assert.Equal(expected, TargetingRules.WeightedPick([(a, 1.0), (b, 1.0)], new ScriptedRandomSource(roll))!.ID);
    }

    [Fact]
    public void TargetWeight_StateMultipliersAreExclusive()
    {
        var shooter = TestEntityFactory.Screen("dd");
        var target = TestEntityFactory.Screen("target");

        target.CurrentStatus = ShipStatus.Active;
        Assert.Equal(6 * 1.5, TargetingRules.TargetWeight(shooter, WeaponSlot.Light, target, defines), 9);

        target.CurrentStatus = ShipStatus.Retreating;
        Assert.Equal(6 * 0.5, TargetingRules.TargetWeight(shooter, WeaponSlot.Light, target, defines), 9);
    }

    [Fact]
    public void TargetWeight_DamagedShipsUpToDoubleAndDamagedConvoysUpToElevenfold()
    {
        var shooter = TestEntityFactory.Submarine("ss");
        var ship = TestEntityFactory.Screen("target");
        ship.CurrentHP = 0.0001;
        var convoy = TestEntityFactory.CreateShip("convoy", ShipRole.Convoy, TestEntityFactory.ScreenStats());
        convoy.CurrentHP = convoy.GetFinalStats().Hp / 2;

        Assert.Equal(3 * 2, TargetingRules.TargetWeight(shooter, WeaponSlot.Torpedo, ship, defines), 3);
        Assert.Equal(600 * (1 + 10 * 0.5), TargetingRules.TargetWeight(shooter, WeaponSlot.Torpedo, convoy, defines), 6);
    }

    [Fact]
    public void SelectTarget_HiddenSubmarinesCannotBeTargetedButRevealedOnesCanByTheLightSlot()
    {
        var hunter = TestEntityFactory.CreateShip("dd", ShipRole.Screen, TestEntityFactory.ScreenStats() with { DepthChargeAttack = 10 });
        var submarine = TestEntityFactory.Submarine("ss");

        var (own, enemy) = TestEntityFactory.Sides([hunter], [submarine]);
        Assert.Null(TargetingRules.SelectTarget(hunter, WeaponSlot.Light, own, enemy, defines, new ScriptedRandomSource(0.0)));

        // Building sides resets combat state, so reveal the submarine afterwards.
        (own, enemy) = TestEntityFactory.Sides([hunter], [submarine]);
        submarine.RevealTimer = 8;
        Assert.Same(submarine, TargetingRules.SelectTarget(hunter, WeaponSlot.Light, own, enemy, defines, new ScriptedRandomSource(0.0)));
    }

    [Fact]
    public void SelectTarget_TorpedoWithNothingInReachDoesNotFire()
    {
        var shooter = TestEntityFactory.Screen("dd", torpedoAttack: 5);
        var (own, enemy) = TestEntityFactory.Sides([shooter], [TestEntityFactory.Capital("bb")]);

        // No enemy screens and screening 1: the roll keeps the torpedo on line 1, which is empty.
        enemy.Screening = new ScreeningSummary(1, 1);

        Assert.Null(TargetingRules.SelectTarget(shooter, WeaponSlot.Torpedo, own, enemy, defines, new ScriptedRandomSource(0.5)));
    }

    [Fact]
    public void WeightedPick_TestsTheHeaviestCandidateFirst()
    {
        var light = TestEntityFactory.Screen("light");
        var heavy = TestEntityFactory.Screen("heavy");
        (Ship, double)[] candidates = [(light, 1), (heavy, 3)];

        Assert.Same(heavy, TargetingRules.WeightedPick(candidates, new ScriptedRandomSource(0.0)));
        Assert.Same(heavy, TargetingRules.WeightedPick(candidates, new ScriptedRandomSource(0.74)));
        Assert.Same(light, TargetingRules.WeightedPick(candidates, new ScriptedRandomSource(0.76)));
    }
}
