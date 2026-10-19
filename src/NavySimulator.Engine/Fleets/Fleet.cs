namespace NavySimulator.Engine;

public class Fleet
{
    public string ID;
    public List<Ship> Ships;

    /// <summary>A-WINGS: the air wings on each carrier, by ship ID; carriers left out have none.</summary>
    public Dictionary<string, List<CarrierAirwingAssignment>> CarrierAirwingsByShip;

    public Fleet(
        string id,
        List<Ship> ships,
        Dictionary<string, List<CarrierAirwingAssignment>>? carrierAirwingsByShip = null)
    {
        ID = id;
        Ships = ships;
        CarrierAirwingsByShip = carrierAirwingsByShip ?? new Dictionary<string, List<CarrierAirwingAssignment>>(StringComparer.Ordinal);
    }
}
