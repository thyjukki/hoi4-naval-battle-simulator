using NavySimulator.Engine.Stats;

namespace NavySimulator.Engine;

public class StatModule(
    string id,
    ShipStats statModifiers,
    ShipStats statMultipliers,
    ShipStats? statAverages = null,
    string category = "",
    IReadOnlyList<string>? addedEquipmentTypes = null,
    IReadOnlyList<string>? forbiddenCategories = null)
{
    public string ID { get; } = id;
    public ShipStats StatModifiers { get; } = statModifiers;
    public ShipStats StatMultipliers { get; } = statMultipliers;
    public ShipStats StatAverages { get; } = statAverages ?? new ShipStats();

    /// <summary>The module <c>category</c>, matched against slots' allowed categories (S-DESIGN-SLOT).</summary>
    public string Category { get; } = category;

    /// <summary><c>add_equipment_type</c>: equipment types the design gains, e.g. <c>capital_ship</c> (S-DESIGN-TYPE).</summary>
    public IReadOnlyList<string> AddedEquipmentTypes { get; } = addedEquipmentTypes ?? [];

    /// <summary><c>forbid_module_categories</c>: categories no other slot of the design may hold (S-DESIGN-FORBID).</summary>
    public IReadOnlyList<string> ForbiddenCategories { get; } = forbiddenCategories ?? [];
}
