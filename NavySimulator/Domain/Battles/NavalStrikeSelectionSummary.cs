namespace NavySimulator.Domain.Battles;

internal sealed record NavalStrikeSelectionSummary(
    Dictionary<string, int> TargetSelections,
    Dictionary<string, int> CarrierTargetSelections,
    Dictionary<string, int> CarrierBombersShotDownByShipId,
    Dictionary<string, int> CarrierBombersShotDownByWingKey,
    int BombersShotDown,
    int CarrierBombersShotDown,
    double TotalDamageDealt,
    double TotalOrganizationDamageDealt,
    double CarrierDamageDealt,
    double CarrierOrganizationDamageDealt,
    double CarrierAverageTargetAaDefense,
    double CarrierAverageCombinedFleetAaDamageReduction,
    Dictionary<string, double> DamageByPlaneType);

