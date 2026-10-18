using NavySimulator.Engine.Battles;
using NavySimulator.Engine.Battles.Mechanics;
using NavySimulator.Engine.Defines;
using NavySimulator.Engine.Tests.TestSupport;

namespace NavySimulator.Engine.Tests.Mechanics;

public class RetreatAndSubmarineTests
{
    private readonly GameDefines defines = GameDefines.Vanilla();

    [Fact]
    public void HpThreshold_FollowsAggressivenessAndHalvesForCapitalsAndCarriers()
    {
        var screen = TestEntityFactory.Screen("dd");
        var capital = TestEntityFactory.Capital("bb");

        Assert.Equal(0.9, RetreatRules.HpThreshold(screen, Aggressiveness.DoNotEngage, defines), 9);
        Assert.Equal(0.9, RetreatRules.HpThreshold(screen, Aggressiveness.EngageAtLowRisk, defines), 9);
        Assert.Equal(0.6, RetreatRules.HpThreshold(screen, Aggressiveness.EngageAtMediumRisk, defines), 9);
        Assert.Equal(0.4, RetreatRules.HpThreshold(screen, Aggressiveness.AlwaysEngage, defines), 9);
        Assert.Equal(0.3, RetreatRules.HpThreshold(capital, Aggressiveness.EngageAtMediumRisk, defines), 9);
    }

    [Fact]
    public void TryStartRetreat_WaitsForMinimumDurationAndRolls()
    {
        var ship = TestEntityFactory.Screen("dd");
        ship.CurrentStatus = ShipStatus.Active;
        ship.CurrentHP = 50;

        Assert.False(RetreatRules.TryStartRetreat(ship, 7, Aggressiveness.EngageAtMediumRisk, defines, new ScriptedRandomSource(0.0)));
        Assert.False(RetreatRules.TryStartRetreat(ship, 8, Aggressiveness.EngageAtMediumRisk, defines, new ScriptedRandomSource(0.5)));
        Assert.True(RetreatRules.TryStartRetreat(ship, 8, Aggressiveness.EngageAtMediumRisk, defines, new ScriptedRandomSource(0.2)));
        Assert.Equal(ShipStatus.Retreating, ship.CurrentStatus);
    }

    [Fact]
    public void EscapeSpeed_IsAdditiveWithScreeningBonus()
    {
        // 30 kn capital, tick 10, full screening: 0.05 + 10 * 0.015 / 24 + 0.2 + 30 * 0.5 / 100.
        var capital = TestEntityFactory.CreateShip("bb", ShipRole.Capital, TestEntityFactory.CapitalStats() with { Speed = 30 });

        Assert.Equal(0.05 + 0.00625 + 0.2 + 0.15, RetreatRules.EscapeSpeedPerDay(capital, 10, new ScreeningSummary(1, 1), 0, defines), 9);
    }

    [Fact]
    public void EscapeSpeed_SubmarinesAddTheirBonusOnTopOfTheBase()
    {
        var hidden = TestEntityFactory.Submarine("ss");
        var revealed = TestEntityFactory.Submarine("ss2");
        revealed.RevealTimer = 3;
        var baseSpeed = 0.05 + 15 * 0.5 / 100;

        Assert.Equal(baseSpeed + 0.35, RetreatRules.EscapeSpeedPerDay(hidden, 0, new ScreeningSummary(1, 1), 0, defines), 9);
        Assert.Equal(baseSpeed + 0.1, RetreatRules.EscapeSpeedPerDay(revealed, 0, new ScreeningSummary(1, 1), 0, defines), 9);
    }

    [Fact]
    public void RevealChance_MatchesTheDecodedFormula()
    {
        var submarine = TestEntityFactory.Submarine("ss");

        Assert.Equal(20 * 0.065 / (100 - 10), SubmarineRules.RevealChance(submarine, 20, 1.0, false, defines), 9);
        Assert.Equal(20 * 0.065 * 1.1 / (100 - 20), SubmarineRules.RevealChance(submarine, 20, 0.0, true, defines), 9);
    }

    [Fact]
    public void HourlyReveal_StartsHiddenAndStaysRevealedForTheHideTimeout()
    {
        var submarine = TestEntityFactory.Submarine("ss");
        Assert.False(submarine.IsVisible);

        SubmarineRules.HourlyReveal(submarine, 20, 1.0, defines, new ScriptedRandomSource(0.0));
        Assert.Equal(8, submarine.RevealTimer);

        SubmarineRules.HourlyReveal(submarine, 20, 1.0, defines, new ScriptedRandomSource(0.99));
        Assert.Equal(7, submarine.RevealTimer);
    }
}
