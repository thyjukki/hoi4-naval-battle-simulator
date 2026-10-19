using NavySimulator.Engine.Stats;

namespace NavySimulator.Engine.Leaders;

/// <summary>
/// S-ADMIRAL-TRAIT: a unit leader trait an admiral can have (<c>common/unit_leader</c>, <c>type = navy</c> or <c>all</c>).
/// Spec: docs/mechanics/surface.md.
/// </summary>
public sealed class LeaderTrait(
    string id,
    string? traitType,
    ModifierSet modifiers,
    ModifierSet nonSharedModifiers,
    IReadOnlyDictionary<string, LeaderAdjuster> adjusters,
    IReadOnlyDictionary<string, ModifierSet> subUnitModifiers,
    IReadOnlyDictionary<string, LeaderAdjuster> subUnitAdjusters,
    IReadOnlyDictionary<string, int> skills,
    IReadOnlyList<LeaderTraitParent> parents,
    IReadOnlyList<string> mutuallyExclusive,
    IReadOnlyList<string> scripted)
{
    /// <summary>The <c>modifier</c> block key of the night adjuster (attack and defence × darkness).</summary>
    public const string Night = "night";

    public string ID { get; } = id;

    /// <summary>The script's <c>trait_type</c> (e.g. <c>personality_trait</c>); null when it gives none.</summary>
    public string? TraitType { get; } = traitType;

    /// <summary>The <c>modifier</c> block's numbers.</summary>
    public ModifierSet Modifiers { get; } = modifiers;

    /// <summary>The <c>non_shared_modifier</c> block.</summary>
    public ModifierSet NonSharedModifiers { get; } = nonSharedModifiers;

    /// <summary>The <c>modifier</c> block's adjusters: by naval terrain (e.g. <c>water_deep_ocean</c>) and <see cref="Night"/>.</summary>
    public IReadOnlyDictionary<string, LeaderAdjuster> Adjusters { get; } = adjusters;

    /// <summary><c>sub_unit_modifiers</c>: modifiers for the ships (or air wings) of one sub-unit, e.g. <c>destroyer</c>.</summary>
    public IReadOnlyDictionary<string, ModifierSet> SubUnitModifiers { get; } = subUnitModifiers;

    /// <summary>The <c>units = { }</c> adjusters of <c>sub_unit_modifiers</c>, by sub-unit.</summary>
    public IReadOnlyDictionary<string, LeaderAdjuster> SubUnitAdjusters { get; } = subUnitAdjusters;

    /// <summary>Skill levels the trait adds, by the character key (<c>attack_skill</c>, ...).</summary>
    public IReadOnlyDictionary<string, int> Skills { get; } = skills;

    /// <summary>Every requirement must be met to have the trait in the game; none: no condition.</summary>
    public IReadOnlyList<LeaderTraitParent> Parents { get; } = parents;

    public IReadOnlyList<string> MutuallyExclusive { get; } = mutuallyExclusive;

    /// <summary>Scripted effects the simulator does not run (e.g. <c>on_add</c>).</summary>
    public IReadOnlyList<string> Scripted { get; } = scripted;
}
