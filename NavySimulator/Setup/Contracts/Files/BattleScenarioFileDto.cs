using System.Text.Json.Serialization;

namespace NavySimulator.Setup.Contracts;

public class BattleScenarioFileDto
{
    [JsonPropertyName("$schema")]
    public string? Schema { get; set; }

    public BattleScenarioDto BattleScenario { get; set; } = new BattleScenarioDto();
}


