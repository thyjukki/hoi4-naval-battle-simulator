using System.Text.Json.Serialization;

namespace NavySimulator.Setup.Contracts;

public class HullDto
{
    public string ID { get; set; } = string.Empty;
    public string Role { get; set; } = string.Empty;
    [JsonPropertyName("type")]
    public List<string> Types { get; set; } = [];
    public int Manpower { get; set; }
    public ShipStatsDto BaseStats { get; set; } = new ShipStatsDto();
}


