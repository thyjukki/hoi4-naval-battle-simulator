using NavySimulator.Engine.Stats;

namespace NavySimulator.Engine;

public class ShipDesign
{
    public string ID;
    public Hull Hull;
    public ShipType ShipType;
    public List<StatModule> Modules;
    public MioBonus? MioBonus;
    private ShipStats? cachedFinalStats;

    public ShipDesign(string id, Hull hull, ShipType shipType, List<StatModule> modules, MioBonus? mioBonus = null)
    {
        ID = id;
        Hull = hull;
        ShipType = shipType;
        Modules = modules;
        MioBonus = mioBonus;
        Types = new[] { shipType.ID, hull.ID, hull.Archetype }
            .Where(type => !string.IsNullOrEmpty(type))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    /// <summary>S-DESIGN-TYPE: the combat role comes from the ship type, not the hull.</summary>
    public ShipRole Role => ShipType.Role;

    /// <summary>
    /// The IDs that research, MIO and spirit type filters (<c>appliesToTypes</c>) match: the ship type (sub-unit), the
    /// hull and the hull's archetype.
    /// </summary>
    public List<string> Types { get; }

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
    /// Organization is the ship type's <c>max_organisation</c>, added unscaled (S-STATS-SHIP: it is a sub-unit stat).
    /// </summary>
    public ShipStats ComputeStats(IReadOnlyList<ShipStats> equipmentBonuses)
    {
        var mio = MioBonus?.GetEquipmentBonus(Role, Types) ?? new ShipStats();
        var add = Modules.Aggregate(Hull.BaseStats, (current, module) => current.Add(module.StatModifiers))
            .Add(ShipStats.AverageNonZero(Modules.Select(module => module.StatAverages)))
            .Add(mio.HitChanceFactorsOnly());
        var multiplier = Modules.Aggregate(mio.WithoutHitChanceFactors(), (current, module) => current.Add(module.StatMultipliers));
        multiplier = equipmentBonuses.Aggregate(multiplier, (current, bonus) => current.Add(bonus));
        var scaled = add.Scale(multiplier);

        return scaled with
        {
            CarrierSize = Math.Floor(add.CarrierSize + multiplier.CarrierSize),
            Organization = scaled.Organization + ShipType.Organization
        };
    }
}
