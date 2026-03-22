namespace NavySimulator.Setup.Contracts;

public class PlaneDto
{
    public string ID { get; set; } = string.Empty;
    public PlaneStatsDto Stats { get; set; } = new();
}
