namespace NavySimulator.Domain.Battles;

internal sealed class CarrierWingState
{
    public string WingKey;
    public string CarrierShipID;
    public string PlaneID;
    public AirwingType Type;
    public int CurrentPlanes;

    public CarrierWingState(
        string wingKey,
        string carrierShipId,
        string planeId,
        AirwingType type,
        int currentPlanes)
    {
        WingKey = wingKey;
        CarrierShipID = carrierShipId;
        PlaneID = planeId;
        Type = type;
        CurrentPlanes = currentPlanes;
    }
}

