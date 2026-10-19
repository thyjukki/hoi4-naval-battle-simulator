
namespace NavySimulator.GameData.Contracts;

public class ModuleDto
{
    public string ID { get; set; } = string.Empty;

    /// <summary>The game's module name (localisation), e.g. "127 mm Gun".</summary>
    public string? Name { get; set; }

    /// <summary>The game's short name, e.g. "Light Battery III".</summary>
    public string? ShortName { get; set; }

    public string Category { get; set; } = string.Empty;

    /// <summary>The category's title in the game's designer, e.g. "Rapid Fire Guns".</summary>
    public string? CategoryName { get; set; }

    /// <summary><c>add_equipment_type</c>, e.g. <c>capital_ship</c> (S-DESIGN-TYPE).</summary>
    public List<string> AddEquipmentTypes { get; set; } = [];

    /// <summary><c>forbid_module_categories</c> (S-DESIGN-FORBID).</summary>
    public List<string> ForbiddenCategories { get; set; } = [];

    /// <summary><c>critical_parts</c>: the parts the module adds to the ship's candidates (S-CRIT-PART).</summary>
    public List<string> CriticalParts { get; set; } = [];

    public ShipStatsDto StatModifiers { get; set; } = new();
    public ShipStatsDto StatAverages { get; set; } = new();
    public ShipStatsDto StatMultipliers { get; set; } = new();
}
