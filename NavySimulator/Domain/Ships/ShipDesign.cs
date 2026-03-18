using NavySimulator.Domain.Stats;

namespace NavySimulator.Domain;

public class ShipDesign
{
    public Hull Hull;
    public List<StatModule> Modules;
    public MioBonus? MioBonus;

    public ShipDesign(Hull hull, List<StatModule> modules, MioBonus? mioBonus = null)
    {
        Hull = hull;
        Modules = modules;
        MioBonus = mioBonus;
    }

    public ShipStats GetFinalStats()
    {
        // StatModifiers are additive, averaged statAverages are added once, then statMultipliers are applied.
        var stats = Modules.Aggregate(Hull.BaseStats, (current, module) => current.Add(module.StatModifiers));
        var averagedStats = ShipStats.AverageNonZero(Modules.Select(module => module.StatAverages));
        stats = stats.Add(averagedStats);
        
        stats = Modules.Aggregate(stats, (current, module) => current.Scale(module.StatMultipliers));
        return MioBonus is null ? stats : stats.Scale(MioBonus.PercentBonus);
    }
}
