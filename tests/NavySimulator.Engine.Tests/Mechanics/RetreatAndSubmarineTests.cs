using NavySimulator.Engine.Battles;
using NavySimulator.Engine.Battles.Mechanics;
using NavySimulator.Engine.Defines;
using NavySimulator.Engine.Stats;
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
    public void EscapeSpeed_AddsTheCountryRetreatSpeedOfTheShipsRole()
    {
        // S-RETREAT-SPEED / S-STATS-COUNTRY: warships read naval_retreat_speed, submarines sub_retreat_speed, convoys convoy_retreat_speed.
        var countryModifiers = new ModifierSet(new Dictionary<string, double>
        {
            ["naval_retreat_speed"] = 0.2,
            ["sub_retreat_speed"] = 0.3,
            ["convoy_retreat_speed"] = 0.4
        });
        var screen = TestEntityFactory.Screen("dd");
        var submarine = TestEntityFactory.Submarine("ss");
        var convoy = TestEntityFactory.CreateShip("cv", ShipRole.Convoy, new ShipStats(Speed: 10, Hp: 100));
        screen.CountryModifiers = countryModifiers;
        submarine.CountryModifiers = countryModifiers;
        convoy.CountryModifiers = countryModifiers;
        var noScreening = new ScreeningSummary(0, 0);

        Assert.Equal(0.05 + 0.2 + 30 * 0.5 / 100, RetreatRules.EscapeSpeedPerDay(screen, 0, noScreening, 0, defines), 9);
        Assert.Equal(0.05 + 0.3 + 15 * 0.5 / 100 + 0.35, RetreatRules.EscapeSpeedPerDay(submarine, 0, noScreening, 0, defines), 9);
        Assert.Equal(0.05 + 0.4 + 10 * 0.5 / 100, RetreatRules.EscapeSpeedPerDay(convoy, 0, noScreening, 0, defines), 9);
    }

    [Fact]
    public void EscapeSpeed_AddsTheAfterInitialCombatModifierOnlyAfterCombatInitialDuration()
    {
        var screen = TestEntityFactory.Screen("dd");
        screen.CountryModifiers = new ModifierSet(new Dictionary<string, double> { ["naval_retreat_speed_after_initial_combat"] = 0.1 });
        var noScreening = new ScreeningSummary(0, 0);

        Assert.Equal(0.05 + 6 * 0.015 / 24 + 0.15, RetreatRules.EscapeSpeedPerDay(screen, 6, noScreening, 0, defines), 9);
        Assert.Equal(0.05 + 7 * 0.015 / 24 + 0.15 + 0.1, RetreatRules.EscapeSpeedPerDay(screen, 7, noScreening, 0, defines), 9);
    }

    [Fact]
    public void Participant_GivesEveryShipItsResearchesCountryModifiers_WhateverTheirStatScope()
    {
        // TFB's smoke_generator plus a research whose stat blocks are for another ship type: country modifiers are not scoped.
        var smokeGenerator = CreateResearch("smoke_generator", [], new Dictionary<string, double> { ["naval_retreat_speed"] = 0.2 });
        var scoped = CreateResearch("dd_only", ["Destroyer"], new Dictionary<string, double> { ["naval_retreat_speed"] = 0.05 });
        var screen = TestEntityFactory.Screen("cl");

        var participant = new BattleParticipant(new Fleet("f", [screen]), string.Empty, string.Empty, 0, [smokeGenerator, scoped], []);

        Assert.Equal(0.25, participant.CountryModifiers.Get("naval_retreat_speed"), 9);
        Assert.Equal(0.25, screen.CountryModifiers.Get("naval_retreat_speed"), 9);
        Assert.Equal(0.05 + 0.25 + 0.15, RetreatRules.EscapeSpeedPerDay(screen, 0, new ScreeningSummary(0, 0), 0, defines), 9);
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

    private static Research CreateResearch(string id, List<string> appliesToTypes, Dictionary<string, double> countryModifiers)
    {
        return new Research(id, new ShipStats(), new ShipStats(), new ShipStats(), [], appliesToTypes, countryModifiers: new ModifierSet(countryModifiers));
    }
}
