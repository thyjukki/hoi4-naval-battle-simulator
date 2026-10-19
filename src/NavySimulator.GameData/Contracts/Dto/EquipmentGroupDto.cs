namespace NavySimulator.GameData.Contracts;

/// <summary>
/// An equipment group of <c>common/equipment_groups</c>: a name for a set of equipment types, archetypes, categories or
/// other groups, used by MIOs and equipment bonuses (S-MIO-MATCH).
/// </summary>
public class EquipmentGroupDto
{
    public string ID { get; set; } = string.Empty;

    /// <summary>The game's name (localisation), e.g. "All Battleship".</summary>
    public string? Name { get; set; }

    /// <summary><c>equipment_type</c>, as written (groups are not expanded).</summary>
    public List<string> EquipmentTypes { get; set; } = [];
}
