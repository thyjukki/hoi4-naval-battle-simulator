using System.Text.Json.Serialization;

namespace NavySimulator.GameData.Contracts;

public class BattleScenarioFileDto
{
    [JsonPropertyName("$schema")]
    public string? Schema { get; set; }

    public BattleScenarioDto BattleScenario { get; set; } = new BattleScenarioDto();
}


