using NavySimulator.Engine.Battles;
using NavySimulator.Engine.Battles.Air;
using NavySimulator.Engine.Defines;
using NavySimulator.Engine.Tests.TestSupport;

namespace NavySimulator.Engine.Tests.Mechanics;

public class AirRulesTests
{
    private readonly GameDefines defines = GameDefines.Vanilla();

    private static PlaneEquipment Bomber() => new("bomber", new PlaneStats(AirDefense: 10, AirAttack: 5, NavalAttack: 20, Speed: 400, Agility: 40, NavalTargeting: 10, Reliability: 0.8),
        ["naval_bomber"], ["naval_bomber"], carrierCapable: true);
    private static PlaneEquipment Fighter() => new("fighter", new PlaneStats(AirDefense: 20, AirAttack: 30, Speed: 500, Agility: 50, Reliability: 0.8),
        ["fighter", "interceptor"], ["air_superiority", "interception"], carrierCapable: true);

    private static Ship Carrier(double deckSize = 3)
    {
        return TestEntityFactory.CreateShip("cv", ShipRole.Carrier, TestEntityFactory.CapitalStats() with { HeavyAttack = 0, CarrierSize = deckSize });
    }

    [Theory]
    [InlineData(4, true)]
    [InlineData(12, true)]
    [InlineData(20, true)]
    [InlineData(5, false)]
    [InlineData(0, false)]
    public void IsAirHour_FollowsTheRegionClock(int hourOfDay, bool expected)
    {
        Assert.Equal(expected, AirRules.IsAirHour(hourOfDay, 4, defines));
    }

    [Fact]
    public void SortieEfficiency_UsesTheStanceForTheFirstDayThenTheBase()
    {
        var group = AirGroup.ForCarrier(Carrier(), [new AirWing(Bomber(), 30, defines.Hardcoded)]);
        var fullScreening = new ScreeningSummary(1, 1);

        // Stance index 4 (ratio 1.0): attacker uses it, defender 1 - 1.0; both get +0.1 from capital_screening_bonus.
        Assert.Equal(1.1, AirRules.SortieEfficiency(group, fullScreening, true, 4, 0, 0, defines), 9);
        Assert.Equal(0.1, AirRules.SortieEfficiency(group, fullScreening, false, 4, 0, 0, defines), 9);
        Assert.Equal(0.6, AirRules.SortieEfficiency(group, fullScreening, true, 4, 30, 0, defines), 9);
    }

    [Fact]
    public void SortieEfficiency_IsZeroAtNightAndScalesWithOrganization()
    {
        var carrier = Carrier();
        var group = AirGroup.ForCarrier(carrier, [new AirWing(Bomber(), 30, defines.Hardcoded)]);
        var noScreening = new ScreeningSummary(0, 0);

        Assert.Equal(0, AirRules.SortieEfficiency(group, noScreening, true, 2, 30, 1, defines), 9);

        carrier.CurrentOrganization = carrier.GetFinalStats().Organization / 2;
        Assert.Equal(0.25, AirRules.SortieEfficiency(group, noScreening, true, 2, 30, 0, defines), 9);
    }

    [Fact]
    public void SortieEfficiency_AtNightUsesTheLoadedNightBlockAndCountryNightTraffic()
    {
        // TFB: night carrier_traffic = -0.75; carrier doctrine carrier_night_traffic = +0.15.
        var tfb = GameDefines.Vanilla();
        tfb.StaticModifiers.Blocks[StaticModifiers.Night][StaticModifiers.CarrierTraffic] = -0.75;
        var group = AirGroup.ForCarrier(Carrier(), [new AirWing(Bomber(), 30, defines.Hardcoded)]);
        var noScreening = new ScreeningSummary(0, 0);

        Assert.Equal(0.5 * 0.25, AirRules.SortieEfficiency(group, noScreening, true, 2, 30, 1, tfb), 9);
        Assert.Equal(0.5 * 0.40, AirRules.SortieEfficiency(group, noScreening, true, 2, 30, 1, tfb, carrierNightTraffic: 0.15), 9);
        Assert.Equal(0.5, AirRules.SortieEfficiency(group, noScreening, true, 2, 30, 0, tfb, carrierNightTraffic: 0.15), 9);
    }

    [Theory]
    [InlineData(30, 1.0)]
    [InlineData(36, 0.6)]
    [InlineData(45, 0.0)]
    [InlineData(60, 0.0)]
    public void CrowdFactor_PenalisesOverfillingTheDeck(int planes, double expected)
    {
        Assert.Equal(expected, AirRules.CrowdFactor(Carrier(deckSize: 3), planes, defines), 9);
    }

    [Fact]
    public void PlaneCap_IsTheLargerOfTheMinimumAndTheSilhouette()
    {
        Ship[] smallFleet = [TestEntityFactory.Capital("bb1"), TestEntityFactory.Capital("bb2"), .. Enumerable.Range(0, 4).Select(i => TestEntityFactory.Screen($"dd{i}"))];
        var carriers = Enumerable.Range(0, 6).Select(i => TestEntityFactory.Carrier($"cv{i}")).ToArray();

        Assert.Equal(80, AirRules.PlaneCap(smallFleet, defines), 9);
        Assert.Equal(96, AirRules.PlaneCap(carriers, defines), 9);
    }

    [Fact]
    public void StackingFactor_IsOneOverOnePlusEffectTimesExcess()
    {
        Assert.Equal(1.0, AirRules.StackingFactor(80, 80, defines), 9);
        Assert.Equal(1.0 / 1.5, AirRules.StackingFactor(180, 80, defines), 9);
    }

    [Fact]
    public void TargetWeight_InterpolatesTypeScoresByScreeningAndAddsTheLowAaTerm()
    {
        var capital = TestEntityFactory.Capital("bb");

        Assert.Equal(500 * 50 + 25, AirRules.TargetWeight(capital, 0, defines), 6);
        Assert.Equal(500 * 10 + 25, AirRules.TargetWeight(capital, 1, defines), 6);
    }

    [Fact]
    public void Interception_FollowsTheDecodedFormula()
    {
        AirWing[] bombers = [new(Bomber(), 10, defines.Hardcoded)];
        AirWing[] fighters = [new(Fighter(), 20, defines.Hardcoded)];

        // D = 0.35 * 20 * 0.5 = 3.5; damage = 3.5 * 0.5 * 30 = 52.5; faster defenders add (1.25 - 1) * 52.5 * 0.65;
        // losses = (52.5 + 8.53125) * 0.01 / 10 = 0.061: none on a roll of 0.5, one on a roll of 0.01.
        Assert.Equal(0, AirRules.Interception(bombers, fighters, 0.5, defines, new ScriptedRandomSource(0.5)));
        Assert.Equal(1, AirRules.Interception(bombers, fighters, 0.5, defines, new ScriptedRandomSource(0.01)));
        Assert.Equal(0, AirRules.Interception(bombers, [], 0.5, defines, new ScriptedRandomSource(0.0)));
    }

    [Fact]
    public void PreemptiveAntiAir_OnlyTheTargetFiresWithTheDecodedChance()
    {
        // chance = 0.2 * (0.9 + 0.5 * (0.9 - 0.8)) = 0.19; kills = 30% of 10 planes.
        Assert.Equal(3, AirRules.PreemptiveAntiAir(30, 0.8, 10, defines, new ScriptedRandomSource(0.1, 0.9)));
        Assert.Equal(0, AirRules.PreemptiveAntiAir(30, 0.8, 10, defines, new ScriptedRandomSource(0.2)));
        Assert.Equal(0, AirRules.PreemptiveAntiAir(0, 0.8, 10, defines, new ScriptedRandomSource(0.0)));
    }

    [Fact]
    public void AntiAirReduction_IsCappedPowerOfThePool()
    {
        Assert.Equal(0.18 * Math.Pow(35, 0.225), AirRules.AntiAirReduction(10, 25, defines), 9);
        Assert.Equal(0.75, AirRules.AntiAirReduction(1000, 0, defines), 9);
        Assert.Equal(0, AirRules.AntiAirReduction(0, 0, defines), 9);
    }

    [Fact]
    public void Hits_AreMultipliedByAUniformRoll()
    {
        // fraction = 1.0 * 0.3; 10 strikers: round(3 * U).
        Assert.Equal(2, AirRules.Hits(10, 1.0, defines, new ScriptedRandomSource(0.5)));
        Assert.Equal(0, AirRules.Hits(10, 1.0, defines, new ScriptedRandomSource(0.1)));
    }

    [Fact]
    public void Criticals_UseTheGunDefinesSpreadOverTheHits()
    {
        var target = TestEntityFactory.Capital("bb");

        Assert.Equal(0.05 * 2 * 0.2, AirRules.CriticalChance(target, 0, defines), 9);
        Assert.Equal(1 + 0.2 * 5 / 2, AirRules.CriticalMultiplier(target, 2, defines), 9);
    }

    [Fact]
    public void CriticalReceiveChance_ActsWithTheOppositeSignOnAirCrits()
    {
        // [RE] 0x197af20: × max(0, 1 − v), so a pride of the fleet's −0.5 makes air crits 1.5 times as likely.
        var target = TestEntityFactory.Capital("bb");

        Assert.Equal(0.05 * 2 * 0.2 * 1.5, AirRules.CriticalChance(target, -0.5, defines), 9);
        Assert.Equal(0.001, AirRules.CriticalChance(target, 2, defines), 9);
    }

    [Fact]
    public void ExternalJoinCap_GrowsWithWholeDays()
    {
        Ship[] enemy = [TestEntityFactory.Capital("bb1"), TestEntityFactory.Capital("bb2")];

        Assert.Equal(50, AirRules.ExternalJoinCap(enemy, 0, defines), 9);
        Assert.Equal(50, AirRules.ExternalJoinCap(enemy, 23, defines), 9);
        Assert.Equal(70, AirRules.ExternalJoinCap(enemy, 48, defines), 9);
    }

    [Fact]
    public void PickTarget_WalksTheListInOrderOnAnIntegerDraw()
    {
        // [RE] 0x24f04f0: p = trunc(U × Σw); the first candidate whose running sum exceeds p, in list order.
        var a = TestEntityFactory.Capital("a");
        var b = TestEntityFactory.Capital("b");
        (Ship, double)[] candidates = [(a, 1), (b, 3)];

        Assert.Same(a, AirRules.PickTarget(candidates, new ScriptedRandomSource(0.24)));
        Assert.Same(b, AirRules.PickTarget(candidates, new ScriptedRandomSource(0.25)));
        Assert.Null(AirRules.PickTarget([(a, 0.0)], new ScriptedRandomSource(0.5)));
    }

    [Fact]
    public void Interception_RollsWhenTheDefendersHaveNoPlanes()
    {
        // [RE + LOG] 0x197b380 returns early only without defending wings; empty wings still make the roll.
        AirWing[] bombers = [new(Bomber(), 10, defines.Hardcoded)];
        AirWing[] empty = [new(Fighter(), 0, defines.Hardcoded)];
        // No damage: x = max(0.001, 0), so a roll below 0.001 still takes one plane.
        Assert.Equal(1, AirRules.Interception(bombers, empty, 0.5, defines, new ScriptedRandomSource(0.0005)));
    }

    [Fact]
    public void FleetAntiAir_CountsEscapedShipsButNotSunkOnes()
    {
        var afloat = TestEntityFactory.Capital("a");
        var escaped = TestEntityFactory.Capital("e");
        var sunk = TestEntityFactory.Capital("s");
        escaped.CurrentStatus = ShipStatus.Retreated;
        sunk.CurrentHP = 0;

        var one = AirRules.FleetAntiAir([afloat], 1, defines);
        Assert.Equal(2 * one, AirRules.FleetAntiAir([afloat, escaped, sunk], 1, defines), 9);
    }

    [Fact]
    public void AceChances_FollowTheDecodedFormula()
    {
        // earn: 0.01 + trunc(trunc(30 × 0.005) × 0.1) = 0.025; death: 0.005 + trunc(trunc(4 × 0.001) × 0.1) = 0.0054.
        Assert.Equal(0.025, AirRules.AceEarnChance(30, 0, defines), 9);
        Assert.Equal(0.0375, AirRules.AceEarnChance(30, 0.5, defines), 9);
        Assert.Equal(0.0054, AirRules.AceDeathChance(4, defines), 9);
    }

    [Fact]
    public void StrikeAverages_AddTheWingsAceAndWeighAttackByMissionEfficiency()
    {
        var ace = new AceTemplate("support_good", new HashSet<string> { "naval_bomber" }, 0.9,
            new Dictionary<string, double> { ["naval_strike_targetting_factor"] = 0.05 });
        var withAce = new AirWing(Bomber(), 10, defines.Hardcoded) { Ace = new AirAce(ace, 0) };
        var plain = new AirWing(Bomber(), 10, defines.Hardcoded);

        // A 10-plane wing doubles the ace (ACE_WING_SIZE 100, at most ×2): targeting 1 × (1 + 0.1) on half the planes.
        var (attack, targeting, _) = AirRules.StrikeAverages([withAce, plain], defines, efficiency: 0.5);
        Assert.Equal(1.05, targeting, 9);
        Assert.Equal(2 * 0.5, attack, 9);

        withAce.Ace.Alive = false;
        Assert.Equal(1.0, AirRules.StrikeAverages([withAce, plain], defines).Targeting, 9);
    }

    [Fact]
    public void RegionStrikeEfficiency_FollowsTheDecodedFormula()
    {
        // [RE + LOG] 0xf78160 with vanilla defines: den = 10 × (1 + 0.1 × 0.5) × (1 + 0.05 × 0.5) × (1 + 0.5 × 1) = 16.14375.
        var wing = new AirWing(Bomber(), 10, defines.Hardcoded);
        var den = 10 * 1.05 * 1.025 * 1.5;

        Assert.Equal(1 - Math.Truncate(Math.Sqrt(20 / den) * 0.316228 * 1e5) / 1e5, RegionAirWarRules.StrikeEfficiency(20, 10, wing, defines), 9);
        Assert.Equal(1, RegionAirWarRules.StrikeEfficiency(0, 10, wing, defines), 9);
    }

    [Fact]
    public void RegionLosses_AreRolledBelowTheCapAndTakenWholeAboveIt()
    {
        Assert.Equal(1, RegionAirWarRules.Losses(0.4, 5, new ScriptedRandomSource(0.3)));
        Assert.Equal(0, RegionAirWarRules.Losses(0.4, 5, new ScriptedRandomSource(0.5)));
        Assert.Equal(3, RegionAirWarRules.Losses(7.2, 3, new ScriptedRandomSource(0.0)));
        Assert.Equal(0, RegionAirWarRules.Losses(7.2, 0, new ScriptedRandomSource(0.0)));
    }

    [Fact]
    public void RegionDisruption_IsCancelledByTheVictimsEscortsOnlyWhenTheyAreStrongEnough()
    {
        var fighter = RegionAirWarRules.Stats(new AirWing(Fighter(), 10, defines.Hardcoded), defines);

        // Vanilla: disruption 6 × 10 × (1 + 500/800); escort 2 × 10 × (1 + 500/800) × (1 + 0.5) × (1 + 0.3).
        Assert.Equal(6 * 10 * 1.625, RegionAirWarRules.Disruption(10, fighter, true, defines), 6);
        Assert.Equal(2 * 10 * 1.625 * 1.5 * 1.3, RegionAirWarRules.Escort(10, fighter, defines), 6);
    }
}
