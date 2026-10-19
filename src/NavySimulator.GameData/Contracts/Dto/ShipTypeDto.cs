namespace NavySimulator.GameData.Contracts;

/// <summary>A naval sub-unit of <c>common/units</c>; see S-DESIGN-TYPE.</summary>
public class ShipTypeDto
{
    public string ID { get; set; } = string.Empty;

    /// <summary>The game's display name (localisation), e.g. "Light Cruiser".</summary>
    public string? Name { get; set; }

    public string Role { get; set; } = string.Empty;

    /// <summary>The sub-unit's <c>type</c> block, e.g. <c>screen_ship</c>.</summary>
    public List<string> UnitTypes { get; set; } = [];

    public int Priority { get; set; }

    /// <summary>Hull archetypes (or hulls) from <c>need</c> / <c>need_equipment</c>.</summary>
    public List<string> Hulls { get; set; } = [];

    /// <summary><c>need_equipment_modules = { any = { ... } }</c>.</summary>
    public List<string> RequiredModules { get; set; } = [];

    /// <summary><c>max_organisation</c>.</summary>
    public double Organization { get; set; }
}
