using NavySimulator.Engine.Stats;

namespace NavySimulator.Engine;

public class StatModule(
    string id,
    ShipStats statModifiers,
    ShipStats statMultipliers,
    ShipStats? statAverages = null)
{
    public string ID { get; } = id;
    public ShipStats StatModifiers { get; } = statModifiers;
    public ShipStats StatMultipliers { get; } = statMultipliers;
    public ShipStats StatAverages { get; } = statAverages ?? new ShipStats();
}


