using NavySimulator.Engine.Stats;

namespace NavySimulator.Engine.Mios;

/// <summary>A trait of an MIO's tree, or its initial trait (S-MIO-TREE).</summary>
public sealed class MioTrait(
    string id,
    IReadOnlyList<MioTraitParent> parents,
    IReadOnlyList<string> mutuallyExclusive,
    IReadOnlyList<string> limitToEquipmentTypes,
    ShipStats equipmentBonus,
    IReadOnlyList<string> notSimulated)
{
    /// <summary><c>token</c>; empty for an initial trait.</summary>
    public string ID { get; } = id;

    /// <summary>Every requirement must be met to unlock the trait; none: a root.</summary>
    public IReadOnlyList<MioTraitParent> Parents { get; } = parents;

    public IReadOnlyList<string> MutuallyExclusive { get; } = mutuallyExclusive;

    /// <summary><c>limit_to_equipment_type</c>; empty: every design the MIO is assigned to (S-MIO-MATCH).</summary>
    public IReadOnlyList<string> LimitToEquipmentTypes { get; } = limitToEquipmentTypes;

    /// <summary><c>equipment_bonus</c>, one value per stat (S-STATS-MIO).</summary>
    public ShipStats EquipmentBonus { get; } = equipmentBonus;

    /// <summary>Bonus keys with no simulator stat, e.g. <c>fuel_consumption</c>.</summary>
    public IReadOnlyList<string> NotSimulated { get; } = notSimulated;
}
