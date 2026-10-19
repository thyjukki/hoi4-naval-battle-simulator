namespace NavySimulator.GameData.Contracts;

public class ModuleSlotDto
{
    public string ID { get; set; } = string.Empty;

    /// <summary>The game's slot title (localisation), e.g. "Battery".</summary>
    public string? Name { get; set; }

    public bool Required { get; set; }
    public List<string> AllowedCategories { get; set; } = [];

    /// <summary>
    /// The game's iteration order of design slots: the slot name's first appearance among the equipment files
    /// (<c>common/units/equipment</c>, in load order). Critical parts are picked in this order (S-CRIT-PART).
    /// Null: the hull's own slot order.
    /// </summary>
    public int? GameOrder { get; set; }
}
