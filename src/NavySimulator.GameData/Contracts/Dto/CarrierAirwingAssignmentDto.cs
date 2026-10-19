namespace NavySimulator.GameData.Contracts;

/// <summary>A-WINGS: <c>airwings</c> wings of <c>planeCount</c> planes (0: 10) of one plane type on a carrier.</summary>
public class CarrierAirwingAssignmentDto
{
    public string PlaneID { get; set; } = string.Empty;
    public int Airwings { get; set; } = 1;
    public int PlaneCount { get; set; }
}
