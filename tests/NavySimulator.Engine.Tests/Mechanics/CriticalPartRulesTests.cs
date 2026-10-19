using NavySimulator.Engine.Battles;
using NavySimulator.Engine.Battles.Mechanics;
using NavySimulator.Engine.Defines;
using NavySimulator.Engine.Stats;
using NavySimulator.Engine.Tests.TestSupport;

namespace NavySimulator.Engine.Tests.Mechanics;

public class CriticalPartRulesTests
{
    private readonly GameDefines defines = GameDefines.Vanilla();
    private readonly BattleRules noRandomness = new() { DamageRandomness = DamageRandomnessModel.None };

    [Fact]
    public void Candidates_MergeAPartAcrossModules_AddingInstancesUpToItsMaximum()
    {
        var fire = Part("on_fire");
        var guns = Part("damaged_guns", damageInstancePerAddedModule: 1, maxDamageInstance: 2);
        var radar = Part("radar", baseDamageInstance: -1);
        var design = Design(shipTypeParts: [fire], moduleParts: [[guns], [guns, radar], [guns, radar], [fire]]);

        Assert.Equal(
            [("on_fire", 1), ("damaged_guns", 2), ("radar", -1)],
            design.CriticalPartCandidates.Select(candidate => (candidate.Part.ID, candidate.MaxInstances)));
    }

    [Theory]
    [InlineData(0.0, "heavy")]
    [InlineData(0.66666, "heavy")]
    [InlineData(0.66667, "propeller")]
    [InlineData(0.99999, "propeller")]
    public void DamageRandomPart_WalksTheWeightsInCandidateOrder(double roll, string expected)
    {
        // Σ chance 1.5 in fx5: r = trunc(U × 150000); the heavy guns take r below 0 for r < 100000.
        var ship = Ship(Design(moduleParts: [[Part("heavy")], [Part("propeller", chance: 0.5)]]));

        var part = CriticalPartRules.DamageRandomPart(ship, defines, new ScriptedRandomSource(roll));

        Assert.Equal(expected, part!.ID);
        Assert.Equal(1, ship.DamageCount(part));
    }

    [Fact]
    public void DamageRandomPart_SkipsPartsDamagedTheirMaximumNumberOfTimes()
    {
        var heavy = Part("heavy");
        var propeller = Part("propeller", maxDamageInstance: 1);
        var ship = Ship(Design(moduleParts: [[heavy], [propeller]]));
        ship.DamagePart(heavy);

        Assert.Equal(propeller, CriticalPartRules.DamageRandomPart(ship, defines, new ScriptedRandomSource(0.0)));
        Assert.Null(CriticalPartRules.DamageRandomPart(ship, defines, new ScriptedRandomSource(0.0)));
    }

    [Fact]
    public void RollPartDamage_IsCertainWithoutARollWhenTheChanceReachesOne()
    {
        var ship = Ship(Design(moduleParts: [[Part("heavy")]], stats: TestEntityFactory.CapitalStats() with { Reliability = 0.05 }));
        var random = new RecordingRandomSource(1);

        Assert.True(CriticalPartRules.RollPartDamage(ship, defines, random));
        Assert.Empty(random.Rolls);
    }

    [Theory]
    [InlineData(0.12499, true)]
    [InlineData(0.125, false)]
    public void RollPartDamage_ChanceIsTheDefineTimesTheShipTypeMultiplierOverReliability(double roll, bool expected)
    {
        // 0.1 × 1 / 0.8 = 0.125.
        var ship = Ship(Design(moduleParts: [[Part("heavy")]]));

        Assert.Equal(expected, CriticalPartRules.RollPartDamage(ship, defines, new ScriptedRandomSource(roll)));
    }

    [Fact]
    public void RollHit_ADamagedPartReplacesTheCriticalMultiplier()
    {
        var fire = Part("on_fire", strDamageMultiplier: 0.5, orgDamageMultiplier: 2, orgDamage: 5);
        var target = Ship(Design(moduleParts: [[fire]], stats: TestEntityFactory.ScreenStats() with { Hp = 100, Organization = 50 }));
        target.CurrentHP = 50;
        var profile = new ShotProfile(10, 0, 1, 1.0, 2.0, null, false);

        // Rolls: critical, part damage, part pick.
        var hit = DamageRules.RollHit(target, profile, defines, noRandomness, new ScriptedRandomSource(0.0));

        Assert.True(hit.IsCritical);
        Assert.True(hit.IsPartHit);
        Assert.Equal(10 * 0.6 * 0.5, hit.StrDamage, 9);
        Assert.Equal(10 * 0.5 * 2 + 5, hit.OrgDamage, 9);
        Assert.Equal(1, target.DamageCount(fire));
    }

    [Fact]
    public void StatPenalties_ScaleTheAttacksByCount_AndLeaveDetectionAndHitChanceAlone()
    {
        var guns = Part("heavy", damageInstancePerAddedModule: 1, statPenalties: new ShipStats(HeavyAttack: -0.5));
        var radar = Part("radar", statPenalties: new ShipStats(SurfaceDetection: -5, LightHitChanceFactor: -0.1));
        var ship = Ship(Design(moduleParts: [[guns], [guns], [radar]], stats: TestEntityFactory.CapitalStats() with { LightHitChanceFactor = 0.2 }));

        ship.DamagePart(guns);
        Assert.Equal(20, ship.GetFinalStats().HeavyAttack, 9);

        ship.DamagePart(guns);
        ship.DamagePart(radar);
        Assert.Equal(0, ship.GetFinalStats().HeavyAttack, 9);
        Assert.Equal(20, ship.GetFinalStats().SurfaceDetection, 9);
        Assert.Equal(0.2, ship.GetFinalStats().LightHitChanceFactor, 9);
    }

    [Fact]
    public void PartModifiers_AreScaledByTheCriticalEffectFactor()
    {
        // [CAL] A broken propeller (-0.9) under TFB's damage control (-0.2) leaves 0.28 of the speed.
        var propeller = Part("propeller", modifiers: new([new(StaticModifiers.NavalSpeedFactor, -0.9)]));
        var ship = Ship(Design(moduleParts: [[propeller]]));
        ship.CountryModifiers = new([new(StaticModifiers.NavalCriticalEffectFactor, -0.2)]);

        ship.DamagePart(propeller);

        Assert.Equal(25 * 0.28, CombatStatRules.CombatSpeed(ship, defines), 9);
    }

    [Fact]
    public void CombatSpeed_KeepsAtLeastATenthOfTheSpeed()
    {
        var propeller = Part("propeller", modifiers: new([new(StaticModifiers.NavalSpeedFactor, -0.9)]));
        var ship = Ship(Design(moduleParts: [[propeller], [propeller]]));

        ship.DamagePart(propeller);
        ship.DamagePart(propeller);

        Assert.Equal(25 * 0.1, CombatStatRules.CombatSpeed(ship, defines), 9);
    }

    [Fact]
    public void AFire_HalvesTheMaximumOrganization_AndCutsItsRegain()
    {
        var fire = Part("on_fire", modifiers: new([new(StaticModifiers.NavyOrgFactor, -0.5), new(StaticModifiers.NavalMoraleFactor, -0.8)]));
        var ship = Ship(Design(moduleParts: [[fire]]));

        CriticalPartRules.DamageRandomPart(ship, defines, new ScriptedRandomSource(0.0));

        Assert.Equal(20, CombatStatRules.MaxOrganization(ship, defines), 9);
        Assert.Equal(20, ship.CurrentOrganization, 9);

        ship.CurrentOrganization = 10;
        ship.CurrentStatus = ShipStatus.Retreating;
        Assert.Equal(defines.NMilitary.SHIP_MORALE_TO_ORG_REGAIN_BASE * 0.2, ExperienceRules.HourlyOrganizationRegain(ship, defines), 9);
    }

    [Fact]
    public void AJammedRudder_LowersTheRetreatDecisionChance()
    {
        var rudder = Part("rudder_jammed", modifiers: new([new(StaticModifiers.NavalRetreatChance, -0.9)]));
        var ship = Ship(Design(moduleParts: [[rudder]]));

        ship.DamagePart(rudder);

        Assert.Equal(defines.NNavy.COMBAT_RETREAT_DECISION_CHANCE * 0.1, RetreatRules.RetreatDecisionChance(ship, 1, defines), 9);
    }

    [Fact]
    public void ResetCombatState_RepairsTheParts()
    {
        var guns = Part("heavy", statPenalties: new ShipStats(HeavyAttack: -0.5));
        var ship = Ship(Design(moduleParts: [[guns]]));
        ship.DamagePart(guns);

        ship.ResetCombatState();

        Assert.Empty(ship.DamagedParts);
        Assert.Equal(40, ship.GetFinalStats().HeavyAttack, 9);
    }

    private static CriticalPart Part(
        string id,
        double chance = 1,
        double strDamageMultiplier = 1,
        double orgDamageMultiplier = 1,
        double orgDamage = 0,
        int baseDamageInstance = 1,
        int damageInstancePerAddedModule = 0,
        int maxDamageInstance = -1,
        ShipStats? statPenalties = null,
        ModifierSet? modifiers = null)
    {
        return new CriticalPart(id, chance, strDamageMultiplier, orgDamageMultiplier, 0, orgDamage, baseDamageInstance,
            damageInstancePerAddedModule, maxDamageInstance, statPenalties ?? new ShipStats(), modifiers ?? ModifierSet.Empty);
    }

    private static ShipDesign Design(List<CriticalPart>? shipTypeParts = null, List<List<CriticalPart>>? moduleParts = null, ShipStats? stats = null)
    {
        var hull = new Hull("hull", "archetype", string.Empty, 400, stats ?? TestEntityFactory.CapitalStats());
        var shipType = new ShipType("type", ShipRole.Capital, [], 0, [hull.Archetype], [], 0, shipTypeParts);
        var modules = (moduleParts ?? []).Select((parts, index) => new StatModule($"module_{index}", new ShipStats(), new ShipStats(), criticalParts: parts)).ToList();
        return new ShipDesign("design", hull, shipType, modules);
    }

    private static Ship Ship(ShipDesign design)
    {
        return new Ship("ship", design, 0);
    }
}
