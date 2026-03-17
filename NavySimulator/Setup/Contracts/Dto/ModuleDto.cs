namespace NavySimulator.Setup.Contracts;

public class ModuleDto
{
    public string ID { get; set; } = string.Empty;
    public ShipStatsDto StatModifiers { get; set; } = new ShipStatsDto();
}


