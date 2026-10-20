using NavySimulator.Engine.Battles;
using NavySimulator.Engine.Battles.Mechanics;
using NavySimulator.Engine.Defines;
using NavySimulator.Engine.Environment;
using NavySimulator.Engine.Leaders;
using NavySimulator.Engine.Stats;
using NavySimulator.Engine.Tests.TestSupport;

namespace NavySimulator.Engine.Tests.Mechanics;

/// <summary>S-POTF [RE]: the pride of the fleet's static block (0xc387e2) and the admiral's in-combat terms (0xc351ae).</summary>
public class PrideOfTheFleetRulesTests
{
    private readonly GameDefines defines = GameDefines.Vanilla();

    [Fact]
    public void ThePrideOfTheFleet_CarriesTheStaticBlock_AndTheOtherShipsDoNot()
    {
        var pride = TestEntityFactory.Capital("bb1");
        var other = TestEntityFactory.Capital("bb2");
        _ = new BattleParticipant(new Fleet("fleet", [pride, other]), null, [], [], prideOfTheFleet: pride);

        Assert.True(pride.IsPrideOfTheFleet);
        Assert.False(other.IsPrideOfTheFleet);
        Assert.Equal(-0.5, MemberModifierRules.ShipModifier(pride, StaticModifiers.CriticalReceiveChance, BattleEnvironment.None, defines), 9);
        Assert.Equal(0, MemberModifierRules.ShipModifier(other, StaticModifiers.CriticalReceiveChance, BattleEnvironment.None, defines));
    }

    [Fact]
    public void WhileThePrideFights_EveryShipGetsTheAdmiralsAttackAndDefense()
    {
        var pride = TestEntityFactory.Capital("bb");
        var screen = TestEntityFactory.Screen("dd");
        _ = new BattleParticipant(new Fleet("fleet", [pride, screen]), MediaPersonality(), [], [], prideOfTheFleet: pride);
        var plainAttack = CombatValueWithout(screen, false);
        var plainArmor = CombatValueWithout(screen, true);

        Assert.Equal(40 * (plainAttack / 40 + 0.1), CombatStatRules.CombatValue(screen, 40, false, BattleEnvironment.None, defines), 9);
        Assert.Equal(40 * (plainArmor / 40 + 0.15), CombatStatRules.CombatValue(screen, 40, true, BattleEnvironment.None, defines), 9);

        pride.CurrentStatus = ShipStatus.Retreating;
        Assert.Equal(40 * (plainAttack / 40 + 0.1), CombatStatRules.CombatValue(screen, 40, false, BattleEnvironment.None, defines), 9);

        // [RE] An escaped pride's task force is still in the combat.
        pride.CurrentStatus = ShipStatus.Retreated;
        Assert.Equal(40 * (plainAttack / 40 + 0.1), CombatStatRules.CombatValue(screen, 40, false, BattleEnvironment.None, defines), 9);

        pride.CurrentStatus = ShipStatus.Active;
        pride.CurrentHP = 0;
        Assert.Equal(plainArmor, CombatStatRules.CombatValue(screen, 40, true, BattleEnvironment.None, defines), 9);
    }

    [Fact]
    public void WithoutAPride_TheAdmiralsTermsDoNothing()
    {
        var screen = TestEntityFactory.Screen("dd");
        _ = new BattleParticipant(new Fleet("fleet", [screen]), MediaPersonality(), [], []);

        Assert.Equal(CombatValueWithout(screen, false), CombatStatRules.CombatValue(screen, 40, false, BattleEnvironment.None, defines), 9);
    }

    [Fact]
    public void ThePride_GainsExperienceAQuarterFaster_WithTheCountrysGainModifiers()
    {
        var pride = TestEntityFactory.Capital("bb1");
        var other = TestEntityFactory.Capital("bb2");
        _ = new BattleParticipant(new Fleet("fleet", [pride, other]), null, [], [], prideOfTheFleet: pride);
        other.CountryModifiers = pride.CountryModifiers = new ModifierSet([
            new(StaticModifiers.ExperienceGainNavyUnit, 0.1), new(StaticModifiers.ExperienceGainNavyUnitFactor, 0.2),
            new(ExperienceRules.SubUnitCombatFactor("Capital"), 0.05)
        ]);

        Assert.Equal((1 + 0.1) * (1 + 0.2 + 0.05), ExperienceRules.ModifiedGain(other, 1, defines), 9);
        Assert.Equal((1 + 0.1) * (1 + 0.2 + 0.05 + 0.25), ExperienceRules.ModifiedGain(pride, 1, defines), 9);
    }

    [Theory]
    [InlineData("capital_ship", true)]
    [InlineData("carrier", true)]
    [InlineData("screen_ship", false)]
    [InlineData("submarine", false)]
    public void OnlyCapitalShipsAndCarriers_CanBeThePride(string unitType, bool eligible)
    {
        var hull = new Hull("hull", "archetype", string.Empty, 400, TestEntityFactory.CapitalStats());
        var design = new ShipDesign("design", hull, new ShipType("type", ShipRole.Capital, [unitType], 0, ["archetype"], [], 40), []);

        Assert.Equal(eligible, PrideOfTheFleetRules.CanBePrideOfTheFleet(design));
    }

    [Fact]
    public void ThePride_MustBeAShipOfTheFleet()
    {
        Assert.Throws<ArgumentException>(() =>
            new BattleParticipant(new Fleet("fleet", [TestEntityFactory.Capital("bb")]), null, [], [], prideOfTheFleet: TestEntityFactory.Capital("other")));
    }

    // The same ship and admiral without a pride of the fleet.
    private double CombatValueWithout(Ship ship, bool isArmor)
    {
        var pride = ship.PrideOfTheFleet;
        ship.PrideOfTheFleet = null;
        var value = CombatStatRules.CombatValue(ship, 40, isArmor, BattleEnvironment.None, defines);
        ship.PrideOfTheFleet = pride;
        return value;
    }

    // Vanilla's navy_media_personality has 0.1 / 0.1; the defense is 0.15 here to tell the two apart.
    private static Admiral MediaPersonality()
    {
        var trait = new LeaderTrait("navy_media_personality", "personality_trait",
            new ModifierSet([new(StaticModifiers.NavalHasPotfInCombatAttack, 0.1), new(StaticModifiers.NavalHasPotfInCombatDefense, 0.15)]),
            ModifierSet.Empty, new Dictionary<string, LeaderAdjuster>(), new Dictionary<string, ModifierSet>(), new Dictionary<string, LeaderAdjuster>(),
            new Dictionary<string, int>(), [], [], []);
        return new Admiral("test", "Test", 1, 0, 0, 0, 0, [trait], LeaderSkills.None);
    }
}
