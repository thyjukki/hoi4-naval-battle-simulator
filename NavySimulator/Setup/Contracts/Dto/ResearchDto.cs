namespace NavySimulator.Setup.Contracts;

public class ResearchDto
{
    public string ID { get; set; } = string.Empty;
    public ShipStatsDto StatModifiers { get; set; } = new();
    public ShipStatsDto StatAverages { get; set; } = new();
    public ShipStatsDto StatMultipliers { get; set; } = new();
    public List<string> AppliesToRoles { get; set; } = [];
    public List<string> AppliesToTypes { get; set; } = [];
}

