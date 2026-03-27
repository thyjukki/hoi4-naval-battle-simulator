namespace NavySimulator.Domain.Battles;

internal sealed class NavalStrikeSelectionSummary
{
    public Dictionary<string, int> TargetSelections;
    public Dictionary<string, int> CarrierTargetSelections;
    public Dictionary<string, int> CarrierBombersShotDownByShipId;
    public Dictionary<string, int> CarrierBombersShotDownByWingKey;
    public int BombersShotDown;
    public int CarrierBombersShotDown;
    public double TotalDamageDealt;
    public double TotalOrganizationDamageDealt;
    public double CarrierDamageDealt;
    public double CarrierOrganizationDamageDealt;
    public double CarrierAverageTargetAaDefense;
    public double CarrierAverageCombinedFleetAaDamageReduction;
    public Dictionary<string, double> DamageByPlaneType;

    public NavalStrikeSelectionSummary(
        Dictionary<string, int> targetSelections,
        Dictionary<string, int> carrierTargetSelections,
        Dictionary<string, int> carrierBombersShotDownByShipId,
        Dictionary<string, int> carrierBombersShotDownByWingKey,
        int bombersShotDown,
        int carrierBombersShotDown,
        double totalDamageDealt,
        double totalOrganizationDamageDealt,
        double carrierDamageDealt,
        double carrierOrganizationDamageDealt,
        double carrierAverageTargetAaDefense,
        double carrierAverageCombinedFleetAaDamageReduction,
        Dictionary<string, double> damageByPlaneType)
    {
        TargetSelections = targetSelections;
        CarrierTargetSelections = carrierTargetSelections;
        CarrierBombersShotDownByShipId = carrierBombersShotDownByShipId;
        CarrierBombersShotDownByWingKey = carrierBombersShotDownByWingKey;
        BombersShotDown = bombersShotDown;
        CarrierBombersShotDown = carrierBombersShotDown;
        TotalDamageDealt = totalDamageDealt;
        TotalOrganizationDamageDealt = totalOrganizationDamageDealt;
        CarrierDamageDealt = carrierDamageDealt;
        CarrierOrganizationDamageDealt = carrierOrganizationDamageDealt;
        CarrierAverageTargetAaDefense = carrierAverageTargetAaDefense;
        CarrierAverageCombinedFleetAaDamageReduction = carrierAverageCombinedFleetAaDamageReduction;
        DamageByPlaneType = damageByPlaneType;
    }
}

