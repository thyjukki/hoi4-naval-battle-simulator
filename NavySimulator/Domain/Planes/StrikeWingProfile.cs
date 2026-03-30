namespace NavySimulator.Domain.Battles;

internal sealed record StrikeWingProfile(
    string WingKey,
    string CarrierShipID,
    string PlaneTypeLabel,
    int SortiePlanes,
    bool IsCarrierBased);

