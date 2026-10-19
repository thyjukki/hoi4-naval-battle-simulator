namespace NavySimulator.GameData.Contracts;

/// <summary>A trait of an MIO's tree, or its initial trait (S-MIO-TREE). Bonus keys are the game's.</summary>
public class MioTraitDto
{
    /// <summary><c>token</c>; empty for an initial trait.</summary>
    public string ID { get; set; } = string.Empty;

    /// <summary>The game's name (localisation).</summary>
    public string? Name { get; set; }

    /// <summary>Column in the tree grid (<c>position</c>, with <c>relative_position_id</c> resolved).</summary>
    public double X { get; set; }

    /// <summary>Row in the tree grid.</summary>
    public double Y { get; set; }

    /// <summary><c>special_trait_background</c>: drawn golden in the game.</summary>
    public bool Special { get; set; }

    /// <summary>
    /// <c>parent</c>, <c>any_parent</c> and <c>all_parents</c>: every requirement must be met to unlock the trait. A trait
    /// with none is a root.
    /// </summary>
    public List<MioTraitParentDto> Parents { get; set; } = [];

    /// <summary><c>mutually_exclusive</c>: traits that cannot be unlocked together with this one.</summary>
    public List<string> MutuallyExclusive { get; set; } = [];

    /// <summary>
    /// <c>limit_to_equipment_type</c>: the bonuses reach only designs of these equipment types, archetypes or groups;
    /// empty reaches every design the MIO is assigned to (S-MIO-MATCH).
    /// </summary>
    public List<string> LimitToEquipmentTypes { get; set; } = [];

    /// <summary><c>equipment_bonus</c>: design stat bonuses by stat key, e.g. <c>armor_value = 0.05</c> (S-STATS-MIO).</summary>
    public Dictionary<string, double> EquipmentBonus { get; set; } = [];

    /// <summary><c>production_bonus</c>: production line bonuses; no effect in battle.</summary>
    public Dictionary<string, double> ProductionBonus { get; set; } = [];

    /// <summary><c>organization_modifier</c>: modifiers of the MIO itself (research, funds); no effect in battle.</summary>
    public Dictionary<string, double> OrganizationModifiers { get; set; } = [];

    /// <summary>The <c>visible</c> and <c>available</c> triggers as script text; the simulator does not check them.</summary>
    public List<string> Conditions { get; set; } = [];
}
