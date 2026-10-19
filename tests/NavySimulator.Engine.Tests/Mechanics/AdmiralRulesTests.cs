using NavySimulator.Engine.Battles;
using NavySimulator.Engine.Battles.Mechanics;
using NavySimulator.Engine.Defines;
using NavySimulator.Engine.Leaders;
using NavySimulator.Engine.Stats;
using NavySimulator.Engine.Tests.TestSupport;

namespace NavySimulator.Engine.Tests.Mechanics;

/// <summary>S-ADMIRAL [RE]: an admiral's containers (0xc21fc0 / 0xc21570) and where combat reads them.</summary>
public class AdmiralRulesTests
{
    private readonly GameDefines defines = GameDefines.Vanilla();

    [Fact]
    public void CombatModifiers_SumTraitsAndSkillLevels_WhileTraitModifiersHaveNoSkills()
    {
        var tactician = Trait("superior_tactician",
            modifiers: Set(("naval_hit_chance", 0.03)),
            nonShared: Set(("navy_org", 5)));

        var admiral = Admiral(attack: 2, maneuvering: 1, traits: [tactician]);

        Assert.Equal(0.03, admiral.CombatModifiers.Get("naval_hit_chance"), 9);
        Assert.Equal(5, admiral.CombatModifiers.Get("navy_org"), 9);
        Assert.Equal(0.10, admiral.CombatModifiers.Get("naval_damage_factor"), 9);
        Assert.Equal(0.025, admiral.CombatModifiers.Get("positioning"), 9);
        Assert.Equal(0.03, admiral.TraitModifiers.Get("naval_hit_chance"), 9);
        Assert.Equal(5, admiral.TraitModifiers.Get("navy_org"), 9);
        Assert.Equal(0, admiral.TraitModifiers.Get("naval_damage_factor"));
    }

    [Fact]
    public void SkillLevels_AddTheTraitsAdds_AndAreReadBetweenLevelOneAndTheHighest()
    {
        var demoted = Trait("demoted", skills: new Dictionary<string, int> { [LeaderSkills.Attack] = -1 });

        Assert.Equal(0.15, Admiral(attack: 7).CombatModifiers.Get("naval_damage_factor"), 9);
        Assert.Equal(0.05, Admiral(attack: 2, traits: [demoted]).CombatModifiers.Get("naval_damage_factor"), 9);
        Assert.Equal(1, Admiral(attack: 2, traits: [demoted]).SkillLevels[LeaderSkills.Attack]);
        // [RE] 0xc24870: a level of 0 or below reads level 1.
        Assert.Equal(-1, Admiral(attack: 0, traits: [demoted]).SkillLevels[LeaderSkills.Attack]);
        Assert.Equal(0.05, Admiral(attack: 0, traits: [demoted]).CombatModifiers.Get("naval_damage_factor"), 9);
    }

    [Fact]
    public void SkillBonusFactor_ScalesPositiveIsGoodKeys_AndOnlyTheBenefitOfPositiveIsBadKeys()
    {
        var wounded = Trait("wounded", nonShared: Set(("skill_bonus_factor", -0.5)));
        var craven = Trait("craven", modifiers: Set(("naval_retreat_chance", 0.25), ("naval_damage_factor", -0.05)));
        var hidden = Trait("hidden", modifiers: Set(("navy_visibility", -0.1), ("critical_receive_chance", 0.2)));
        var leader = Trait("leader", subUnit: new Dictionary<string, ModifierSet> { ["destroyer"] = Set(("naval_speed_factor", 0.1)) });

        var admiral = Admiral(attack: 2, traits: [wounded, craven, hidden, leader]);

        Assert.Equal(0.5, admiral.Efficiency, 9);
        Assert.Equal(0.125, admiral.CombatModifiers.Get("naval_retreat_chance"), 9);
        Assert.Equal((0.10 - 0.05) * 0.5, admiral.CombatModifiers.Get("naval_damage_factor"), 9);
        Assert.Equal(-0.05, admiral.CombatModifiers.Get("navy_visibility"), 9);
        Assert.Equal(0.2, admiral.CombatModifiers.Get("critical_receive_chance"), 9);
        Assert.Equal(0.05, admiral.SubUnitModifiers("destroyer").Get("naval_speed_factor"), 9);
        Assert.Equal(0.25, admiral.TraitModifiers.Get("naval_retreat_chance"), 9);
    }

    [Fact]
    public void TheAdmiralsCriticalEffectFactor_ScalesDamagedPartModifiers()
    {
        var crisisMagician = Trait("crisis_magician", modifiers: Set(("naval_critical_effect_factor", -0.5)));
        var propeller = new CriticalPart("propeller", 1, 1, 1, 0, 0, 1, 0, -1, new ShipStats(), Set(("naval_speed_factor", -0.6)));
        var destroyer = Ship("destroyer");
        _ = new BattleParticipant(new Fleet("fleet", [destroyer]), Admiral(traits: [crisisMagician]), [], []);

        destroyer.DamagePart(propeller);

        Assert.Equal(destroyer.GetFinalStats().Speed * (1 - 0.6 * 0.5), CombatStatRules.CombatSpeed(destroyer, defines), 9);
    }

    [Fact]
    public void ShipModifiers_IncludeTheAdmiral_AndSubUnitModifiersOnlyReachThatSubUnit()
    {
        var leader = Trait("destroyer_leader",
            modifiers: Set(("naval_hit_chance", 0.03)),
            subUnit: new Dictionary<string, ModifierSet> { ["destroyer"] = Set(("naval_speed_factor", 0.1)) });
        var destroyer = Ship("destroyer");
        var cruiser = Ship("light_cruiser");

        _ = new BattleParticipant(new Fleet("fleet", [destroyer, cruiser]), Admiral(traits: [leader]), [], []);

        Assert.Equal(0.03, MemberModifierRules.ShipModifier(destroyer, "naval_hit_chance", 0, defines), 9);
        Assert.Equal(0.03, MemberModifierRules.ShipModifier(cruiser, "naval_hit_chance", 0, defines), 9);
        Assert.Equal(destroyer.GetFinalStats().Speed * 1.1, CombatStatRules.CombatSpeed(destroyer, defines), 9);
        Assert.Equal(cruiser.GetFinalStats().Speed, CombatStatRules.CombatSpeed(cruiser, defines), 9);
    }

    [Fact]
    public void Attack_TakesTheNightAndSubUnitAdjusters_ButArmorDoesNot()
    {
        var nightFighter = Trait("night_fighter",
            adjusters: new Dictionary<string, LeaderAdjuster> { [LeaderTrait.Night] = new(0.2, 0.2, 0) },
            subUnitAdjusters: new Dictionary<string, LeaderAdjuster> { ["destroyer"] = new(0.1, 0.1, 0) });
        var destroyer = Ship("destroyer");
        _ = new BattleParticipant(new Fleet("fleet", [destroyer]), Admiral(traits: [nightFighter]), [], []);

        // Attack and defense level 0 read level 1 (0.05 each); the adjusters reach the attack only.
        Assert.Equal(40 * (1 + 0.05 + 0.2 * 0.5 + 0.1), CombatStatRules.CombatValue(destroyer, 40, false, 0.5, defines), 9);
        Assert.Equal(50 * (1 + 0.05), CombatStatRules.CombatValue(destroyer, 50, true, 0.5, defines), 9);
    }

    [Fact]
    public void FleetSizePenalty_IsScaledByTheEnemyAdmiralsFactor_BeforeTheCap()
    {
        var plain = PositioningRules.FleetSizePenalty(8, 4, defines);

        Assert.Equal(plain * 1.2, PositioningRules.FleetSizePenalty(8, 4, defines, 0.2), 9);
        Assert.Equal(defines.NNavy.MAX_POSITIONING_PENALTY_FROM_HIGHER_SHIP_RATIO, PositioningRules.FleetSizePenalty(40, 4, defines, 0.2), 9);
    }

    [Fact]
    public void RetreatDecisionChance_TakesTheEnemyAdmiralsRetreatChance()
    {
        var ship = TestEntityFactory.Screen("dd");

        Assert.Equal(defines.NNavy.COMBAT_RETREAT_DECISION_CHANCE * 1.2, RetreatRules.RetreatDecisionChance(ship, 1, defines, 0.2), 9);
    }

    [Fact]
    public void Battle_UsesTheAdmiralsPositioning_AndTheirFleetSizePenaltyFactorOnTheEnemy()
    {
        // Eight against four: the attacker's size penalty, raised by the defender admiral's lone_wolf-like factor.
        var loneWolf = Trait("lone_wolf", modifiers: Set(("naval_enemy_fleet_size_ratio_penalty_factor", 0.1)));
        var attackerShips = Screens("a", 8);
        var defenderShips = Screens("d", 4);
        var attacker = new BattleParticipant(new Fleet("a", attackerShips), Admiral(maneuvering: 3), [], []);
        var defender = new BattleParticipant(new Fleet("d", defenderShips), Admiral(traits: [loneWolf]), [], []);
        var scenario = new BattleScenario("test", "ocean", "clear", 1, null, attacker, defender, new Dictionary<string, PlaneEquipment>(), defines, new BattleRules());
        var expected = PositioningRules.Calculate(new BattleLines(attackerShips), new BattleLines(defenderShips), defines, positioningModifier: 0.075,
            enemyFleetSizePenaltyFactor: 0.1);
        var session = new BattleSession(scenario, 1);

        session.StepHour();

        Assert.True(expected < 1);
        Assert.Equal(expected, session.Attacker.Positioning, 9);
        Assert.Equal(defines.NNavy.BASE_POSITIONING, session.Defender.Positioning, 9);
        Assert.Contains(session.Notes, note => note.StartsWith("attacker admiral Test: skill 1, attack 0", StringComparison.Ordinal));
    }

    [Fact]
    public void TraitProblems_ReportMissingParentsAndExclusivePairsOnce()
    {
        var lancer = Trait("lancer", parents: [new LeaderTraitParent(["seawolf", "spotter"], 1)], exclusive: ["silent_hunter"]);
        var hunter = Trait("silent_hunter", exclusive: ["lancer"]);

        var problems = AdmiralRules.TraitProblems(Admiral(traits: [lancer, hunter]));

        Assert.Equal(2, problems.Count);
        Assert.Contains("trait 'lancer' needs 1 of seawolf, spotter", problems);
        Assert.Contains("traits 'lancer' and 'silent_hunter' are mutually exclusive", problems);
    }

    // Three levels per skill: attack naval_damage_factor 0.05 per level, maneuvering positioning 0.025 per level.
    private static LeaderSkills Skills()
    {
        static IReadOnlyDictionary<int, ModifierSet> Levels(string key, double step) =>
            Enumerable.Range(1, 3).ToDictionary(level => level, level => Set((key, step * level)));

        return new LeaderSkills(new Dictionary<string, IReadOnlyDictionary<int, ModifierSet>>
        {
            [LeaderSkills.Attack] = Levels("naval_damage_factor", 0.05),
            [LeaderSkills.Defense] = Levels("naval_defense_factor", 0.05),
            [LeaderSkills.Maneuvering] = Levels("positioning", 0.025)
        });
    }

    private static Admiral Admiral(int attack = 0, int defense = 0, int maneuvering = 0, IReadOnlyList<LeaderTrait>? traits = null)
    {
        return new Admiral("test", "Test", 1, attack, defense, maneuvering, 0, traits ?? [], Skills());
    }

    private static LeaderTrait Trait(
        string id,
        ModifierSet? modifiers = null,
        ModifierSet? nonShared = null,
        Dictionary<string, ModifierSet>? subUnit = null,
        Dictionary<string, LeaderAdjuster>? adjusters = null,
        Dictionary<string, LeaderAdjuster>? subUnitAdjusters = null,
        Dictionary<string, int>? skills = null,
        IReadOnlyList<LeaderTraitParent>? parents = null,
        IReadOnlyList<string>? exclusive = null)
    {
        return new LeaderTrait(id, null, modifiers ?? ModifierSet.Empty, nonShared ?? ModifierSet.Empty,
            adjusters ?? [], subUnit ?? [], subUnitAdjusters ?? [], skills ?? [], parents ?? [], exclusive ?? [], []);
    }

    private static ModifierSet Set(params (string Key, double Value)[] values)
    {
        return new ModifierSet(values.Select(value => KeyValuePair.Create(value.Key, value.Value)));
    }

    private static List<Ship> Screens(string prefix, int count)
    {
        return Enumerable.Range(1, count).Select(index => TestEntityFactory.Screen($"{prefix}{index}")).ToList();
    }

    private static Ship Ship(string subUnit)
    {
        var hull = new Hull("hull", "archetype", string.Empty, 400, TestEntityFactory.ScreenStats());
        // Experience level 1, the base level: no experience bonus or malus.
        var experience = 0.2 * GameDefines.Vanilla().NNavy.NAVY_MAX_XP;
        return new Ship(subUnit, new ShipDesign("design_" + subUnit, hull, new ShipType(subUnit, ShipRole.Screen, ["screen_ship"], 0, ["archetype"], [], 40), []), experience);
    }
}
