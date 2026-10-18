using NavySimulator.Engine.Stats;

namespace NavySimulator.Engine;

public class Research(
    string id,
    ShipStats statModifiers,
    ShipStats statAverages,
    ShipStats statMultipliers,
    List<ShipRole> appliesToRoles,
    List<string> appliesToTypes,
    bool isEquipmentBonus = false)
{
    public string ID { get; } = id;
    public ShipStats StatModifiers { get; } = statModifiers;
    public ShipStats StatAverages { get; } = statAverages;
    public ShipStats StatMultipliers { get; } = statMultipliers;
    public List<ShipRole> AppliesToRoles { get; } = appliesToRoles;
    public List<string> AppliesToTypes { get; } = appliesToTypes;

    /// <summary>
    /// S-STATS-BONUS: an idea or named equipment bonus (e.g. a special project's add_equipment_bonus) that feeds the
    /// design multiplier, instead of a technology's sub-unit bonus applied on the ship (S-STATS-SHIP).
    /// </summary>
    public bool IsEquipmentBonus { get; } = isEquipmentBonus;

    /// <summary>The script's single value per stat; the JSON split into modifiers and multipliers is only notation.</summary>
    public ShipStats Values => StatModifiers.Add(StatMultipliers);

    public bool AppliesTo(ShipRole role, List<string> hullTypes)
    {
        var roleMatch = AppliesToRoles.Count == 0 || AppliesToRoles.Contains(role);
        var typeMatch = AppliesToTypes.Count == 0 || AppliesToTypes.Any(type => hullTypes.Contains(type, StringComparer.OrdinalIgnoreCase));
        return roleMatch && typeMatch;
    }
}

