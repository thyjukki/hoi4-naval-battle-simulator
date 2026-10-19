using System.Text.Json.Serialization;

namespace NavySimulator.GameData.Contracts;

public class ShipDesignDto
{
    public string ID { get; set; } = string.Empty;
    public string HullID { get; set; } = string.Empty;

    /// <summary>Slot ID → module ID. Slots left out are empty.</summary>
    public Dictionary<string, string> Modules { get; set; } = [];

    public string? MioID { get; set; }

    /// <summary>Overrides the designer's module restrictions (allowed categories, forbidden modules, count limits, required slots).</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public bool AllowAnyModule { get; set; }
}
