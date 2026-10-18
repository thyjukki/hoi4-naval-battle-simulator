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
    public readonly RetreatPriority RetreatPriority;
    public readonly SubmarineEngagementRisk SubmarineEngagementRisk;

    public BattleParticipant(
        Fleet fleet,
        string commander,
        string doctrine,
        int externalNavalStrikePlanes,
        List<Research> researches,
        List<Spirit> spirits,
        RetreatPriority retreatPriority = RetreatPriority.Medium,
        SubmarineEngagementRisk submarineEngagementRisk = SubmarineEngagementRisk.Medium)
    {
        Fleet = fleet;
        Commander = commander;
        Doctrine = doctrine;
        ExternalNavalStrikePlanes = externalNavalStrikePlanes;
        Researches = researches;
        Spirits = spirits;
        RetreatPriority = retreatPriority;
        SubmarineEngagementRisk = submarineEngagementRisk;

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
