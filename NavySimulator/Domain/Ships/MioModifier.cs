using NavySimulator.Domain.Stats;

namespace NavySimulator.Domain;

public class MioModifier(
    ShipStats statModifiers,
    ShipStats statAverages,
    ShipStats statMultipliers,
    List<ShipRole> appliesToRoles)
{
    public ShipStats StatModifiers { get; } = statModifiers;
    public ShipStats StatAverages { get; } = statAverages;
    public ShipStats StatMultipliers { get; } = statMultipliers;
    public List<ShipRole> AppliesToRoles { get; } = appliesToRoles;

    public bool AppliesTo(ShipRole role)
    {
        return AppliesToRoles.Count == 0 || AppliesToRoles.Contains(role);
    }
}

