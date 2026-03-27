namespace NavySimulator.Domain.Battles;

internal sealed class AirSortieSnapshot
{
    public bool IsSortieHour;
    public int CarrierAssignedPlanes;
    public int CarrierSortiePlanes;
    public double CarrierSortieEfficiencyMultiplier;
    public double CarrierTrafficMultiplier;
    public int ExternalEligiblePlanes;
    public int ExternalPlanesJoining;
    public double ExternalJoinCap;
    public int BomberWings;
    public int CarrierBomberWings;
    public int ExternalBomberWings;
    public double CarrierBomberAverageAgility;
    public double CarrierBomberAverageNavalAttack;
    public double CarrierBomberAverageNavalTargeting;
    public double ExternalBomberAverageAgility;
    public double ExternalBomberAverageNavalAttack;
    public double ExternalBomberAverageNavalTargeting;
    public IReadOnlyDictionary<string, int> CarrierBomberWingsByPlaneType;
    public IReadOnlyDictionary<string, int> CarrierSortiePlanesByShipId;
    public IReadOnlyList<StrikeWingProfile> CarrierStrikeWingProfiles;

    public AirSortieSnapshot(
        bool isSortieHour,
        int carrierAssignedPlanes,
        int carrierSortiePlanes,
        double carrierSortieEfficiencyMultiplier,
        double carrierTrafficMultiplier,
        int externalEligiblePlanes,
        int externalPlanesJoining,
        double externalJoinCap,
        int bomberWings,
        int carrierBomberWings,
        int externalBomberWings,
        double carrierBomberAverageAgility,
        double carrierBomberAverageNavalAttack,
        double carrierBomberAverageNavalTargeting,
        double externalBomberAverageAgility,
        double externalBomberAverageNavalAttack,
        double externalBomberAverageNavalTargeting,
        IReadOnlyDictionary<string, int> carrierBomberWingsByPlaneType,
        IReadOnlyDictionary<string, int> carrierSortiePlanesByShipId,
        IReadOnlyList<StrikeWingProfile> carrierStrikeWingProfiles)
    {
        IsSortieHour = isSortieHour;
        CarrierAssignedPlanes = carrierAssignedPlanes;
        CarrierSortiePlanes = carrierSortiePlanes;
        CarrierSortieEfficiencyMultiplier = carrierSortieEfficiencyMultiplier;
        CarrierTrafficMultiplier = carrierTrafficMultiplier;
        ExternalEligiblePlanes = externalEligiblePlanes;
        ExternalPlanesJoining = externalPlanesJoining;
        ExternalJoinCap = externalJoinCap;
        BomberWings = bomberWings;
        CarrierBomberWings = carrierBomberWings;
        ExternalBomberWings = externalBomberWings;
        CarrierBomberAverageAgility = carrierBomberAverageAgility;
        CarrierBomberAverageNavalAttack = carrierBomberAverageNavalAttack;
        CarrierBomberAverageNavalTargeting = carrierBomberAverageNavalTargeting;
        ExternalBomberAverageAgility = externalBomberAverageAgility;
        ExternalBomberAverageNavalAttack = externalBomberAverageNavalAttack;
        ExternalBomberAverageNavalTargeting = externalBomberAverageNavalTargeting;
        CarrierBomberWingsByPlaneType = carrierBomberWingsByPlaneType;
        CarrierSortiePlanesByShipId = carrierSortiePlanesByShipId;
        CarrierStrikeWingProfiles = carrierStrikeWingProfiles;
    }
}

