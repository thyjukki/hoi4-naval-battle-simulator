using NavySimulator.Domain.Stats;

namespace NavySimulator.Domain;

public class ShipDesign
{
    public string ID;
    public Hull Hull;
    public List<StatModule> Modules;
    public MioBonus? MioBonus;

    public ShipDesign(string id, Hull hull, List<StatModule> modules, MioBonus? mioBonus = null)
    {
        ID = id;
        Hull = hull;
        Modules = modules;
        MioBonus = mioBonus;
    }

    public ShipStats GetFinalStats()
    {
        var emptyStats = new ShipStats();
        var multipliers = Modules.Aggregate(emptyStats, (current, module) => current.Add(module.StatMultipliers));
        // StatModifiers are additive, averaged statAverages are added once, then statMultipliers are applied.
        var stats = Modules.Aggregate(Hull.BaseStats, (current, module) => current.Add(module.StatModifiers));
        var averagedStats = ShipStats.AverageNonZero(Modules.Select(module => module.StatAverages));
        stats = stats.Add(averagedStats);
        
        stats = stats.Scale(multipliers);
        return MioBonus is null ? stats : stats.Scale(MioBonus.PercentBonus);
    }
}
