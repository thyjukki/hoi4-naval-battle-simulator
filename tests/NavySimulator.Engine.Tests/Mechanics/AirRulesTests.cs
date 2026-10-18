using NavySimulator.Engine.Battles;
using NavySimulator.Engine.Battles.Air;
using NavySimulator.Engine.Defines;
using NavySimulator.Engine.Tests.TestSupport;

namespace NavySimulator.Engine.Tests.Mechanics;

public class AirRulesTests
{
    private readonly GameDefines defines = GameDefines.Vanilla();

    private static PlaneEquipment Bomber() => new("bomber", new PlaneStats(AirDefense: 10, AirAttack: 5, NavalAttack: 20, Speed: 400, Agility: 40, NavalTargeting: 10, Reliability: 0.8));
    private static PlaneEquipment Fighter() => new("fighter", new PlaneStats(AirDefense: 20, AirAttack: 30, Speed: 500, Agility: 50, Reliability: 0.8));

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
        var group = AirGroup.ForCarrier(Carrier(), [new AirWing(Bomber(), AirwingType.Bomber, 30)]);
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
        var group = AirGroup.ForCarrier(carrier, [new AirWing(Bomber(), AirwingType.Bomber, 30)]);
        var noScreening = new ScreeningSummary(0, 0);

        Assert.Equal(0, AirRules.SortieEfficiency(group, noScreening, true, 2, 30, 1, defines), 9);

        carrier.CurrentOrganization = carrier.GetFinalStats().Organization / 2;
        Assert.Equal(0.25, AirRules.SortieEfficiency(group, noScreening, true, 2, 30, 0, defines), 9);
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
        AirWing[] bombers = [new(Bomber(), AirwingType.Bomber, 10)];
        AirWing[] fighters = [new(Fighter(), AirwingType.Fighter, 20)];

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

        Assert.Equal(0.05 * 2 * 0.2, AirRules.CriticalChance(target, defines), 9);
        Assert.Equal(1 + 0.2 * 5 / 2, AirRules.CriticalMultiplier(target, 2, defines), 9);
    }

    [Fact]
    public void ExternalJoinCap_GrowsWithWholeDays()
    {
        Ship[] enemy = [TestEntityFactory.Capital("bb1"), TestEntityFactory.Capital("bb2")];

        Assert.Equal(50, AirRules.ExternalJoinCap(enemy, 0, defines), 9);
        Assert.Equal(50, AirRules.ExternalJoinCap(enemy, 23, defines), 9);
        Assert.Equal(70, AirRules.ExternalJoinCap(enemy, 48, defines), 9);
    }
}
