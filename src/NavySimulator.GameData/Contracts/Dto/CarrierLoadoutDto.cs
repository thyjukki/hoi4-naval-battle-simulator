namespace NavySimulator.GameData.Contracts;

/// <summary>A-WINGS: the air wings on one carrier of a fleet.</summary>
public class CarrierLoadoutDto
{
    public List<CarrierAirwingAssignmentDto> Wings { get; set; } = [];
}
