using System.Text.Json.Serialization;

namespace NavySimulator.GameData.Contracts;

public class OverrideSetFileDto
{
    [JsonPropertyName("$schema")]
    public string? Schema { get; set; }

    public OverrideSetDto OverrideSet { get; set; } = new();
}
