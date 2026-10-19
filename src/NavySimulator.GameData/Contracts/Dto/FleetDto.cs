namespace NavySimulator.GameData.Contracts;

public class FleetDto
{
    public string ID { get; set; } = string.Empty;
    public Dictionary<string, int> ShipDesigns { get; set; } = [];
    public Dictionary<string, int> ShipExperienceLevels { get; set; } = [];

    /// <summary>
    /// A-WINGS: the air wings of each carrier, by design ID: one loadout per ship of the design, in ship order (the first
    /// is ship 001). Carriers without a loadout have no planes.
    /// </summary>
    public Dictionary<string, List<CarrierLoadoutDto>> CarrierAirwings { get; set; } = [];
}


