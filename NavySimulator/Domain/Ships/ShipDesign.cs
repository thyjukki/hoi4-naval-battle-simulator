namespace NavySimulator.Domain;

public class ShipDesign
{
    public Hull Hull;
    public List<IModule> Modules;
    public MioBonus? MioBonus;

    public ShipDesign(Hull hull, List<IModule> modules, MioBonus? mioBonus = null)
    {
        Hull = hull;
        Modules = modules;
        MioBonus = mioBonus;
    }

    public ShipStats GetFinalStats()
    {
        var stats = Modules.Aggregate(Hull.BaseStats, (current, module) => current.Add(module.StatModifiers));

        return MioBonus is null ? stats : stats.Scale(MioBonus.PercentBonus);
    }
}
