namespace NavySimulator.Domain;

public class CarrierAirwingAssignment
{
    public string PlaneID;
    public AirwingType Type;
    public int Airwings;

    public CarrierAirwingAssignment(string planeId, AirwingType type, int airwings)
    {
        PlaneID = planeId;
        Type = type;
        Airwings = airwings;
    }

    public int PlaneCount => Airwings * 10;
}
