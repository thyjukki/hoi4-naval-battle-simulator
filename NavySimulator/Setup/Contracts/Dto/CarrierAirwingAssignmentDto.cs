namespace NavySimulator.Setup.Contracts;

public class CarrierAirwingAssignmentDto
{
    public string PlaneID { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
    public int Airwings { get; set; }
}
