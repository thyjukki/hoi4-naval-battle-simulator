using NavySimulator.Domain.Stats;

namespace NavySimulator.Domain;

public class Research(
    string id,
    ShipStats statModifiers,
    ShipStats statAverages,
    ShipStats statMultipliers,
    List<ShipRole> appliesToRoles)
{
    public string ID { get; } = id;
    public ShipStats StatModifiers { get; } = statModifiers;
    public ShipStats StatAverages { get; } = statAverages;
    public ShipStats StatMultipliers { get; } = statMultipliers;
    public List<ShipRole> AppliesToRoles { get; } = appliesToRoles;

    public bool AppliesTo(ShipRole role)
    {
        return AppliesToRoles.Count == 0 || AppliesToRoles.Contains(role);
    }
}

