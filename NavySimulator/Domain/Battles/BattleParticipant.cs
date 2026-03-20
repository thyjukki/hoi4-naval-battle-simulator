using NavySimulator.Domain.Stats;

namespace NavySimulator.Domain;

public class BattleParticipant
{
    public Fleet Fleet;
    public string Commander;
    public string Doctrine;
    public string TechnologyLevel;
    public string NationModifier;
    public List<Research> Researches;
    public List<Spirit> Spirits;

    public BattleParticipant(
        Fleet fleet,
        string commander,
        string doctrine,
        string technologyLevel,
        string nationModifier,
        List<Research> researches,
        List<Spirit> spirits)
    {
        Fleet = fleet;
        Commander = commander;
        Doctrine = doctrine;
        TechnologyLevel = technologyLevel;
        NationModifier = nationModifier;
        Researches = researches;
        Spirits = spirits;

        ApplyFleetModifiers();
    }

    private void ApplyFleetModifiers()
    {
        foreach (var ship in Fleet.Ships)
        {
            var role = ship.Design.Hull.Role;

            var applicableResearches = Researches.Where(research => research.AppliesTo(role));
            var applicableSpirits = Spirits.Where(spirit => spirit.AppliesTo(role));

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


