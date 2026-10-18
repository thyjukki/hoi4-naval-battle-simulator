using NavySimulator.Engine.Stats;

namespace NavySimulator.Engine;

public class ShipDesign
{
    public string ID;
    public Hull Hull;
    public List<StatModule> Modules;
    public MioBonus? MioBonus;
    private ShipStats? cachedFinalStats;

    public ShipDesign(string id, Hull hull, List<StatModule> modules, MioBonus? mioBonus = null)
    {
        ID = id;
        Hull = hull;
        Modules = modules;
        MioBonus = mioBonus;
    }

    /// <summary>Design stats without country equipment bonuses.</summary>
    public ShipStats GetFinalStats()
    {
        return cachedFinalStats ??= ComputeStats([]);
    }

    /// <summary>
    /// S-STATS-DESIGN [RE] 0xbdfc80: <c>final = (base + Σ add_stats + avg + MIO hit chance factors) × (1 + Σ multiply_stats + MIO + equipment bonuses)</c>.
    /// avg = Σ add_average_stats / number of modules declaring the stat. All multipliers are one additive sum;
    /// MIO light/heavy/torpedo hit chance factors go to the add bucket (S-STATS-MIO). Carrier size's multiplier is a
    /// flat add and the result is floored. Equipment bonuses (idea or named, S-STATS-BONUS) are multiplicative for every stat.
    /// </summary>
    public ShipStats ComputeStats(IReadOnlyList<ShipStats> equipmentBonuses)
    {
        var mio = MioBonus?.GetEquipmentBonus(Hull.Role, Hull.Types) ?? new ShipStats();
        var add = Modules.Aggregate(Hull.BaseStats, (current, module) => current.Add(module.StatModifiers))
            .Add(ShipStats.AverageNonZero(Modules.Select(module => module.StatAverages)))
            .Add(mio.HitChanceFactorsOnly());
        var multiplier = Modules.Aggregate(mio.WithoutHitChanceFactors(), (current, module) => current.Add(module.StatMultipliers));
        multiplier = equipmentBonuses.Aggregate(multiplier, (current, bonus) => current.Add(bonus));

        return add.Scale(multiplier) with { CarrierSize = Math.Floor(add.CarrierSize + multiplier.CarrierSize) };
    }
}
