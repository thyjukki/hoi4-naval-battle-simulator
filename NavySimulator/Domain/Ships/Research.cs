using NavySimulator.Domain.Stats;

namespace NavySimulator.Domain;

public class Research(
    string id,
    ShipStats statModifiers,
    ShipStats statAverages,
    ShipStats statMultipliers,
    List<ShipRole> appliesToRoles,
    List<string> appliesToTypes)
{
    public string ID { get; } = id;
    public ShipStats StatModifiers { get; } = statModifiers;
    public ShipStats StatAverages { get; } = statAverages;
    public ShipStats StatMultipliers { get; } = statMultipliers;
    public List<ShipRole> AppliesToRoles { get; } = appliesToRoles;
    public List<string> AppliesToTypes { get; } = appliesToTypes;

    public bool AppliesTo(ShipRole role, List<string> hullTypes)
    {
        var roleMatch = AppliesToRoles.Count == 0 || AppliesToRoles.Contains(role);
        var typeMatch = AppliesToTypes.Count == 0 || AppliesToTypes.Any(type => hullTypes.Contains(type, StringComparer.OrdinalIgnoreCase));
        return roleMatch && typeMatch;
    }
}

