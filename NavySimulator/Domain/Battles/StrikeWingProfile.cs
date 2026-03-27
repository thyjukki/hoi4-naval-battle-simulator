namespace NavySimulator.Domain.Battles;

internal sealed class StrikeWingProfile
{
    public string WingKey;
    public string CarrierShipID;
    public string PlaneTypeLabel;
    public int SortiePlanes;
    public bool IsCarrierBased;

    public StrikeWingProfile(
        string wingKey,
        string carrierShipId,
        string planeTypeLabel,
        int sortiePlanes,
        bool isCarrierBased)
    {
        WingKey = wingKey;
        CarrierShipID = carrierShipId;
        PlaneTypeLabel = planeTypeLabel;
        SortiePlanes = sortiePlanes;
        IsCarrierBased = isCarrierBased;
    }
}

