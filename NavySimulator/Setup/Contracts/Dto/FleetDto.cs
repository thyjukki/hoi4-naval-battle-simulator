namespace NavySimulator.Setup.Contracts;

public class FleetDto
{
    public string ID { get; set; } = string.Empty;
    public Dictionary<string, int> ShipDesigns { get; set; } = [];
    public Dictionary<string, int> ShipExperienceLevels { get; set; } = [];
    public Dictionary<string, List<CarrierAirwingAssignmentDto>> CarrierAirwings { get; set; } = [];
}


