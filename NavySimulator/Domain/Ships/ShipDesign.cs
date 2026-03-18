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
        // StatModifiers are additive, StatMultipliers are multipliers
        var stats = Modules.Aggregate(Hull.BaseStats, (current, module) => current.Add(module.StatModifiers));
        
        stats = Modules.Aggregate(stats, (current, module) => current.Scale(module.StatMultipliers));
        return MioBonus is null ? stats : stats.Scale(MioBonus.PercentBonus);
    }
}
