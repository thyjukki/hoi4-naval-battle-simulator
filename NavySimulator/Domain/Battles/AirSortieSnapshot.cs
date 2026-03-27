namespace NavySimulator.Domain.Battles;

internal sealed record AirSortieSnapshot(
    bool IsSortieHour,
    int CarrierAssignedPlanes,
    int CarrierSortiePlanes,
    double CarrierSortieEfficiencyMultiplier,
    double CarrierTrafficMultiplier,
    int ExternalEligiblePlanes,
    int ExternalPlanesJoining,
    double ExternalJoinCap,
    int BomberWings,
    int CarrierBomberWings,
    int ExternalBomberWings,
    double CarrierBomberAverageAgility,
    double CarrierBomberAverageNavalAttack,
    double CarrierBomberAverageNavalTargeting,
    double ExternalBomberAverageAgility,
    double ExternalBomberAverageNavalAttack,
    double ExternalBomberAverageNavalTargeting,
    IReadOnlyDictionary<string, int> CarrierBomberWingsByPlaneType,
    IReadOnlyDictionary<string, int> CarrierSortiePlanesByShipId,
    IReadOnlyList<StrikeWingProfile> CarrierStrikeWingProfiles);

