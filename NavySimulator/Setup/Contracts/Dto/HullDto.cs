namespace NavySimulator.Setup.Contracts;

public class HullDto
{
    public string ID { get; set; } = string.Empty;
    public string Role { get; set; } = string.Empty;
    public ShipStatsDto BaseStats { get; set; } = new ShipStatsDto();
}


