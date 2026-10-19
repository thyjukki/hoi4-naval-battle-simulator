using NavySimulator.Engine.Defines;
using NavySimulator.Engine.Stats;

namespace NavySimulator.Engine.Leaders;

/// <summary>
/// S-ADMIRAL: how an admiral's skills and traits become the modifier containers combat reads (CNavyLeader rebuild
/// 0xc21fc0 / 0xc21570; findings S-STATS-COUNTRY-modifier-sources.md and S-ADMIRAL-leader-combat.md). Spec:
/// docs/mechanics/surface.md.
/// </summary>
public static class AdmiralRules
{
    /// <summary>The trait modifier that changes the leader efficiency (e.g. status traits <c>wounded</c>, <c>reassigned</c>).</summary>
    public const string SkillBonusFactor = "skill_bonus_factor";

    /// <summary>
    /// S-ADMIRAL-SKILL [RE] 0xc1e650: the set level plus every trait's add for the skill (e.g. <c>demoted</c> −1); the
    /// level lookup clamps it (<see cref="LeaderSkills.Modifiers"/>).
    /// </summary>
    public static int SkillLevel(int level, IEnumerable<LeaderTrait> traits, string skill)
    {
        return level + traits.Sum(trait => trait.Skills.GetValueOrDefault(skill));
    }

    /// <summary>S-ADMIRAL-MOD [RE] 0xc21fc0: container B, the traits' <c>modifier</c> plus <c>non_shared_modifier</c>.</summary>
    public static ModifierSet TraitModifiers(IEnumerable<LeaderTrait> traits)
    {
        return traits.Aggregate(ModifierSet.Empty, (sum, trait) => sum.Add(trait.Modifiers).Add(trait.NonSharedModifiers));
    }

    /// <summary>
    /// S-ADMIRAL-MOD [RE] 0xc21570, 0xc19fa0: <c>max(0, size factors × task-force cap factor + skill_bonus_factor of
    /// container B)</c>. A navy leader's size factors are always 1 and the cap factor is 1 up to ADMIRAL_TASKFORCE_CAP task
    /// forces, so for one task force this is <c>max(0, 1 + skill_bonus_factor)</c>. (A reassignment cooldown would make
    /// it 0; the side's admiral is taken as assigned long before the battle.)
    /// </summary>
    public static double Efficiency(ModifierSet traitModifiers)
    {
        return Math.Max(0, 1 + traitModifiers.Get(SkillBonusFactor));
    }

    /// <summary>
    /// S-ADMIRAL-MOD [RE] 0xc21fc0 / 0xc21570: container A, the traits' <c>modifier</c> and <c>non_shared_modifier</c> and
    /// the <c>modifier</c> of each skill's level (overall, attack, defense, maneuvering, coordination), scaled by the
    /// efficiency (<see cref="Scale"/>). Medals and leader-bound timed entries are not modelled.
    /// </summary>
    public static ModifierSet CombatModifiers(
        IEnumerable<LeaderTrait> traits,
        IReadOnlyDictionary<string, int> skillLevels,
        LeaderSkills definitions,
        double efficiency,
        HardcodedValues hardcoded)
    {
        var sum = TraitModifiers(traits);

        foreach (var skill in LeaderSkills.NavySkills)
        {
            sum = sum.Add(definitions.Modifiers(skill, skillLevels.GetValueOrDefault(skill)));
        }

        return Scale(sum, efficiency, hardcoded);
    }

    /// <summary>
    /// S-ADMIRAL-MOD [RE] 0x60fbd0: each entry × efficiency by its modifier's flags: a "positive is bad" key
    /// (<see cref="HardcodedValues.LeaderModifiersScaledWhenNegative"/>, e.g. <c>navy_visibility</c>) only where negative,
    /// every other key whatever its sign. With efficiency 1 nothing changes.
    /// </summary>
    public static ModifierSet Scale(ModifierSet modifiers, double efficiency, HardcodedValues hardcoded)
    {
        if (efficiency == 1)
        {
            return modifiers;
        }

        return new ModifierSet(modifiers.Values.Select(entry =>
            entry.Value < 0 || !hardcoded.LeaderModifiersScaledWhenNegative.Contains(entry.Key)
                ? KeyValuePair.Create(entry.Key, entry.Value * efficiency)
                : entry));
    }

    /// <summary>
    /// S-ADMIRAL-TRAIT [PDX]: what the game would not allow: a trait whose parents the admiral lacks, two mutually
    /// exclusive traits. The simulator uses the traits anyway.
    /// </summary>
    public static IReadOnlyList<string> TraitProblems(Admiral admiral)
    {
        var problems = new List<string>();
        var ids = admiral.Traits.Select(trait => trait.ID).ToHashSet(StringComparer.Ordinal);
        var exclusive = new SortedSet<(string, string)>();

        foreach (var trait in admiral.Traits)
        {
            foreach (var parent in trait.Parents.Where(parent => parent.Traits.Count(ids.Contains) < parent.Needed))
            {
                var needed = parent.Needed == parent.Traits.Count && parent.Needed > 1 ? "all" : $"{parent.Needed}";
                problems.Add($"trait '{trait.ID}' needs {needed} of {string.Join(", ", parent.Traits)}");
            }

            foreach (var other in trait.MutuallyExclusive.Where(ids.Contains))
            {
                exclusive.Add(string.CompareOrdinal(trait.ID, other) < 0 ? (trait.ID, other) : (other, trait.ID));
            }
        }

        problems.AddRange(exclusive.Select(pair => $"traits '{pair.Item1}' and '{pair.Item2}' are mutually exclusive"));
        return problems;
    }
}
