namespace NavySimulator.GameData.Contracts;

public class ModuleSlotDto
{
    public string ID { get; set; } = string.Empty;

    /// <summary>The game's slot title (localisation), e.g. "Battery".</summary>
    public string? Name { get; set; }

    public bool Required { get; set; }
    public List<string> AllowedCategories { get; set; } = [];
}
