namespace NavySimulator.Setup.Contracts;

public class MioBonusDto
{
    public string ID { get; set; } = string.Empty;
    public ShipStatsDto PercentBonus { get; set; } = new ShipStatsDto();
}


