namespace NavySimulator.Domain.Battles;

internal sealed class CarrierWingState
{
    public readonly string WingKey;
    public readonly string CarrierShipID;
    public readonly string PlaneID;
    public readonly AirwingType Type;
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

