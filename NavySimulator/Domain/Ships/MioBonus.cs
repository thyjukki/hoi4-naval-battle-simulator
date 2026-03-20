using NavySimulator.Domain.Stats;

namespace NavySimulator.Domain;

public class MioBonus
{
    public string ID;
    public List<MioModifier> Modifiers;

    public MioBonus(string id, List<MioModifier> modifiers)
    {
        ID = id;
        Modifiers = modifiers;
    }

    public (ShipStats StatModifiers, ShipStats StatAverages, ShipStats StatMultipliers) GetCombinedForRoleAndTypes(
        ShipRole role,
        List<string> hullTypes)
    {
        var applicable = Modifiers.Where(modifier => modifier.AppliesTo(role, hullTypes)).ToList();

        var statModifiers = applicable
            .Select(modifier => modifier.StatModifiers)
            .Aggregate(new ShipStats(), (current, value) => current.Add(value));
        var statAverages = ShipStats.AverageNonZero(applicable.Select(modifier => modifier.StatAverages));
        var statMultipliers = applicable
            .Select(modifier => modifier.StatMultipliers)
            .Aggregate(new ShipStats(), (current, value) => current.Add(value));

        return (statModifiers, statAverages, statMultipliers);
    }
}


