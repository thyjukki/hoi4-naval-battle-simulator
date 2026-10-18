using NavySimulator.Engine.Stats;

namespace NavySimulator.Engine;

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

    /// <summary>
    /// S-STATS-MIO [RE] 0xdb7800: the MIO's equipment_bonus as one sum per stat. The script has a single value per
    /// stat; the JSON split into modifiers/multipliers is only notation, so both blocks are summed.
    /// </summary>
    public ShipStats GetEquipmentBonus(ShipRole role, List<string> hullTypes)
    {
        var (statModifiers, _, statMultipliers) = GetCombinedForRoleAndTypes(role, hullTypes);
        return statModifiers.Add(statMultipliers);
    }
}


