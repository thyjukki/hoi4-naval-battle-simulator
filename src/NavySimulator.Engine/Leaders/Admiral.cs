using NavySimulator.Engine.Defines;
using NavySimulator.Engine.Stats;

namespace NavySimulator.Engine.Leaders;

/// <summary>
/// S-ADMIRAL: the admiral commanding a side's fleet: skill levels and traits, and the modifier containers the combat rules
/// read (<see cref="AdmiralRules"/>). Spec: docs/mechanics/surface.md.
/// </summary>
public sealed class Admiral
{
    private readonly Dictionary<string, int> baseSkills;
    private readonly HardcodedValues hardcoded;

    public Admiral(
        string id,
        string name,
        int skill,
        int attackSkill,
        int defenseSkill,
        int maneuveringSkill,
        int coordinationSkill,
        IReadOnlyList<LeaderTrait> traits,
        LeaderSkills skillLevels,
        GameDefines? defines = null)
    {
        hardcoded = (defines ?? GameDefines.Vanilla()).Hardcoded;
        ID = id;
        Name = name;
        Traits = traits;
        baseSkills = new Dictionary<string, int>(StringComparer.Ordinal)
        {
            [LeaderSkills.Skill] = skill,
            [LeaderSkills.Attack] = attackSkill,
            [LeaderSkills.Defense] = defenseSkill,
            [LeaderSkills.Maneuvering] = maneuveringSkill,
            [LeaderSkills.Coordination] = coordinationSkill
        };
        SkillLevels = LeaderSkills.NavySkills.ToDictionary(key => key, key => AdmiralRules.SkillLevel(baseSkills[key], traits, key), StringComparer.Ordinal);
        TraitModifiers = AdmiralRules.TraitModifiers(traits);
        Efficiency = AdmiralRules.Efficiency(TraitModifiers);
        CombatModifiers = AdmiralRules.CombatModifiers(traits, SkillLevels, skillLevels, Efficiency, hardcoded);
    }

    /// <summary>The character the admiral was taken from (e.g. <c>USA_chester_w_nimitz</c>); empty for a made-up one.</summary>
    public string ID { get; }

    public string Name { get; }

    public IReadOnlyList<LeaderTrait> Traits { get; }

    /// <summary>The levels as set, by skill key (<see cref="LeaderSkills.NavySkills"/>), before trait adds.</summary>
    public IReadOnlyDictionary<string, int> BaseSkills => baseSkills;

    /// <summary>S-ADMIRAL-SKILL: the set level plus the traits' adds; the skill files are read at it, clamped (<see cref="LeaderSkills.Modifiers"/>).</summary>
    public IReadOnlyDictionary<string, int> SkillLevels { get; }

    /// <summary>
    /// S-ADMIRAL-MOD [RE] container B (<c>leader+0x298</c>): the traits' <c>modifier</c> and <c>non_shared_modifier</c>,
    /// unscaled. The retreat roll reads the enemy's <c>naval_enemy_retreat_chance</c> from it.
    /// </summary>
    public ModifierSet TraitModifiers { get; }

    /// <summary>S-ADMIRAL-MOD [RE] 0xc19fa0: the leader efficiency <see cref="CombatModifiers"/> is scaled by.</summary>
    public double Efficiency { get; }

    /// <summary>
    /// S-ADMIRAL-MOD [RE] container A (<c>leader+0x418</c>): traits' <c>modifier</c> and <c>non_shared_modifier</c> and the
    /// skill levels' <c>modifier</c>, scaled by <see cref="Efficiency"/>. Every ship modifier read of the fleet includes it.
    /// </summary>
    public ModifierSet CombatModifiers { get; }

    /// <summary>
    /// S-ADMIRAL-MOD [RE] 0xc19bf0, 0xc21570: the traits' <c>sub_unit_modifiers</c> for one sub-unit (e.g. <c>destroyer</c>),
    /// scaled by the efficiency like container A.
    /// </summary>
    public ModifierSet SubUnitModifiers(string subUnit)
    {
        var sum = Traits.Select(trait => trait.SubUnitModifiers.GetValueOrDefault(subUnit))
            .OfType<ModifierSet>()
            .Aggregate(ModifierSet.Empty, (total, modifiers) => total.Add(modifiers));
        return AdmiralRules.Scale(sum, Efficiency, hardcoded);
    }

    // The adjusters are scaled by the size efficiency only (0xc21280), which is 1 for one task force, never by
    // skill_bonus_factor.

    /// <summary>S-ADMIRAL-ADJUST [RE] 0xc19ba0: the traits' <c>units = { }</c> adjusters for one sub-unit.</summary>
    public LeaderAdjuster SubUnitAdjuster(string subUnit)
    {
        return Traits.Aggregate(default(LeaderAdjuster), (sum, trait) => sum.Add(trait.SubUnitAdjusters.GetValueOrDefault(subUnit)));
    }

    /// <summary>S-ADMIRAL-ADJUST [RE]: the traits' terrain adjusters for one naval terrain, or the night adjuster (<see cref="LeaderTrait.Night"/>).</summary>
    public LeaderAdjuster Adjuster(string key)
    {
        return Traits.Aggregate(default(LeaderAdjuster), (sum, trait) => sum.Add(trait.Adjusters.GetValueOrDefault(key)));
    }
}
