namespace NavySimulator.Setup.Contracts;

public class FleetDto
{
    public string ID { get; set; } = string.Empty;
    public Dictionary<string, int> ShipDesigns { get; set; } = [];
}


