namespace NavySimulator.Setup.Contracts;

public class ShipDesignDto
{
    public string ID { get; set; } = string.Empty;
    public string HullID { get; set; } = string.Empty;
    public List<string> ModuleIDs { get; set; } = [];
    public string? MioID { get; set; }
}


