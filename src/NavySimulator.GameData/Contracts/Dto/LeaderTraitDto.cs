namespace NavySimulator.GameData.Contracts;

/// <summary>
/// S-ADMIRAL-TRAIT: a unit leader trait an admiral can have (<c>type = navy</c> or <c>all</c> in
/// <c>common/unit_leader</c>), written by <c>NavySimulator import-leaders</c> to <c>leader-traits/</c>. Keys are the game's.
/// </summary>
public class LeaderTraitDto
{
    public string ID { get; set; } = string.Empty;

    /// <summary>The game's name (localisation), e.g. "Superior Tactician".</summary>
    public string? Name { get; set; }

    /// <summary>
    /// The script's <c>trait_type</c> (<c>personality_trait</c>, <c>basic_trait</c>, <c>status_trait</c>,
    /// <c>assignable_trait</c>, <c>basic_terrain_trait</c>, <c>assignable_terrain_trait</c>); null when it gives none.
    /// </summary>
    public string? TraitType { get; set; }

    /// <summary>The numbers of the <c>modifier</c> block, e.g. <c>naval_damage_factor</c>.</summary>
    public Dictionary<string, double> Modifiers { get; set; } = [];

    /// <summary>The numbers of the <c>non_shared_modifier</c> block.</summary>
    public Dictionary<string, double> NonSharedModifiers { get; set; } = [];

    /// <summary>The blocks of <c>modifier</c>: terrain adjusters by terrain (e.g. <c>water_deep_ocean</c>) and <c>night</c>.</summary>
    public Dictionary<string, LeaderAdjusterDto> Adjusters { get; set; } = [];

    /// <summary>The numbers of each <c>sub_unit_modifiers</c> block, by sub-unit (e.g. <c>destroyer</c>, <c>cv_cas</c>).</summary>
    public Dictionary<string, Dictionary<string, double>> SubUnitModifiers { get; set; } = [];

    /// <summary>The <c>units = { }</c> adjusters inside <c>sub_unit_modifiers</c>, by sub-unit.</summary>
    public Dictionary<string, LeaderAdjusterDto> SubUnitAdjusters { get; set; } = [];

    /// <summary>Skill levels the trait adds, by the game's key (<c>attack_skill</c>, <c>defense_skill</c>, ...).</summary>
    public Dictionary<string, int> Skills { get; set; } = [];

    /// <summary>The trait's <c>parent</c>, <c>any_parent</c> and <c>all_parents</c> conditions (trait tree).</summary>
    public List<LeaderTraitParentDto> Parents { get; set; } = [];

    /// <summary>Traits it cannot be taken with (<c>mutually_exclusive</c>).</summary>
    public List<string> MutuallyExclusive { get; set; } = [];

    /// <summary>The trait's scripted effects the simulator does not run, as script keys (e.g. <c>on_add</c>, <c>enable_ability</c>).</summary>
    public List<string> Scripted { get; set; } = [];
}
