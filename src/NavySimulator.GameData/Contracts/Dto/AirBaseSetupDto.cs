namespace NavySimulator.GameData.Contracts;

/// <summary>
/// A-COVERAGE: one of a side's air bases (by state ID): its air base building level (null: crowding not counted), the
/// planes stationed there besides the side's land-based wings in the scenario, and its air supply ratio (null: 1).
/// </summary>
public class AirBaseSetupDto
{
    public int State { get; set; }
    public int? Level { get; set; }
    public int? OtherPlanes { get; set; }
    public double? SupplyRatio { get; set; }
}
