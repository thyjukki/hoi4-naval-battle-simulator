using NavySimulator.Domain.Stats;

namespace NavySimulator.Domain;

public class StatModule(string id, ShipStats statModifiers, ShipStats statMultipliers)
{
    public string ID { get; } = id;
    public ShipStats StatModifiers { get; } = statModifiers;
    public ShipStats StatMultipliers { get; } = statMultipliers;
}


