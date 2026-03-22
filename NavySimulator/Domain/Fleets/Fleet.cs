
namespace NavySimulator.Domain;

public class Fleet
{
    public string ID;
    public List<Ship> Ships;
    public Dictionary<string, List<CarrierAirwingAssignment>> CarrierAirwingsByShipDesign;

    public Fleet(
        string id,
        List<Ship> ships,
        Dictionary<string, List<CarrierAirwingAssignment>>? carrierAirwingsByShipDesign = null)
    {
        ID = id;
        Ships = ships;
        CarrierAirwingsByShipDesign = carrierAirwingsByShipDesign ?? [];
    }
}


