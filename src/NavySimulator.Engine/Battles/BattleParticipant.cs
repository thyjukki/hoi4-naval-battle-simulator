using NavySimulator.Engine.Stats;

namespace NavySimulator.Engine.Battles;

public class BattleParticipant
{
    public readonly Fleet Fleet;
    public readonly string Commander;
    public readonly string Doctrine;
    public readonly int ExternalNavalStrikePlanes;
    public readonly List<Research> Researches;
    public readonly List<Spirit> Spirits;
    public readonly EngagementLevel EngagementLevel;

    /// <summary>Plane type of the land-based naval strike planes (<see cref="ExternalNavalStrikePlanes"/>).</summary>
    public readonly string? ExternalPlaneId;

    /// <summary>A-EXTERNAL: air mission efficiency of the land-based planes, multiplies their attack.</summary>
    public readonly double ExternalMissionEfficiency;

    /// <summary>A-STANCE: index into CARRIER_OFFENSIVE_STANCE_SORTIE_RATIO; null uses CARRIER_OFFENSIVE_STANCE_DEFAULT_INDEX.</summary>
    public readonly int? CarrierStanceIndex;

    public BattleParticipant(
        Fleet fleet,
        string commander,
        string doctrine,
        int externalNavalStrikePlanes,
        List<Research> researches,
        List<Spirit> spirits,
        EngagementLevel engagementLevel = EngagementLevel.Medium,
        string? externalPlaneId = null,
        double externalMissionEfficiency = 1.0,
        int? carrierStanceIndex = null)
    {
        Fleet = fleet;
        Commander = commander;
        Doctrine = doctrine;
        ExternalNavalStrikePlanes = externalNavalStrikePlanes;
        Researches = researches;
        Spirits = spirits;
        EngagementLevel = engagementLevel;
        ExternalPlaneId = externalPlaneId;
        ExternalMissionEfficiency = externalMissionEfficiency;
        CarrierStanceIndex = carrierStanceIndex;

        ApplyFleetModifiers();
    }

    private void ApplyFleetModifiers()
    {
        foreach (var ship in Fleet.Ships)
        {
            var role = ship.Design.Hull.Role;
            var hullTypes = ship.Design.Hull.Types;

            var applicableResearches = Researches.Where(research => research.AppliesTo(role, hullTypes)).ToList();
            var applicableSpirits = Spirits.Where(spirit => spirit.AppliesTo(role, hullTypes)).ToList();

            var modifiers = applicableResearches.Select(research => research.StatModifiers)
                .Concat(applicableSpirits.Select(spirit => spirit.StatModifiers));
            var averages = applicableResearches.Select(research => research.StatAverages)
                .Concat(applicableSpirits.Select(spirit => spirit.StatAverages));
            var multipliers = applicableResearches.Select(research => research.StatMultipliers)
                .Concat(applicableSpirits.Select(spirit => spirit.StatMultipliers));

            var combinedModifiers = modifiers.Aggregate(new ShipStats(), (current, value) => current.Add(value));
            var combinedAverages = ShipStats.AverageNonZero(averages);
            var combinedMultipliers = multipliers.Aggregate(new ShipStats(), (current, value) => current.Add(value));

            ship.ApplyExternalModifiers(combinedModifiers, combinedAverages, combinedMultipliers);
        }
    }
}
