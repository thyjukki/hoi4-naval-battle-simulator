using NavySimulator.Engine.Mios;
using NavySimulator.Engine.Stats;

namespace NavySimulator.Engine;

public class ShipDesign
{
    public string ID;
    public Hull Hull;
    public ShipType ShipType;
    public List<StatModule> Modules;

    /// <summary>S-MIO: the MIO the design is assigned to (its design team), if any.</summary>
    public Mio? Mio;
    private ShipStats? cachedFinalStats;

    /// <param name="equipmentGroups">
    /// The equipment groups (<c>common/equipment_groups</c>), each with its members expanded; the design belongs to every
    /// group that holds its hull, archetype or one of its equipment types (S-MIO-MATCH).
    /// </param>
    public ShipDesign(
        string id,
        Hull hull,
        ShipType shipType,
        List<StatModule> modules,
        Mio? mio = null,
        IReadOnlyDictionary<string, IReadOnlySet<string>>? equipmentGroups = null)
    {
        ID = id;
        Hull = hull;
        ShipType = shipType;
        Modules = modules;
        Mio = mio;
        Types = new[] { shipType.ID, hull.ID, hull.Archetype }
            .Where(type => !string.IsNullOrEmpty(type))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
        var equipment = new[] { hull.ID, hull.Archetype }.Concat(EquipmentKeywords(hull))
            .Where(type => !string.IsNullOrEmpty(type))
            .Distinct(StringComparer.Ordinal)
            .ToList();
        EquipmentBonusTypes = equipment
            .Concat((equipmentGroups ?? new Dictionary<string, IReadOnlySet<string>>())
                .Where(group => equipment.Any(group.Value.Contains))
                .Select(group => group.Key))
            .Distinct(StringComparer.Ordinal)
            .ToList();
        CriticalPartCandidates = MergeCriticalParts(shipType, modules);
    }

    /// <summary>
    /// S-CRIT-PART [RE + CAL] 0xc31030: the parts a critical hit can damage on the design's ships, in pick order: the
    /// ship type's, then each module's in <see cref="Modules"/> order (the game's slot order, ModuleSlot.GameOrder). The first time a part comes up it can be damaged
    /// <c>base_damage_instance</c> times; each further module with it adds <c>damage_instance_per_added_module</c>, up to
    /// <c>max_damage_instance</c> (-1 stays unlimited).
    /// </summary>
    public IReadOnlyList<CriticalPartCandidate> CriticalPartCandidates { get; }

    /// <summary>S-DESIGN-TYPE: the combat role comes from the ship type, not the hull.</summary>
    public ShipRole Role => ShipType.Role;

    /// <summary>
    /// The IDs that technology-like (sub-unit) bonus filters (<c>appliesToTypes</c>) match: the ship type (sub-unit), the
    /// hull and the hull's archetype.
    /// </summary>
    public List<string> Types { get; }

    /// <summary>
    /// S-MIO-MATCH, S-STATS-BONUS [RE] 0xa0a600, 0xc97e00: what an equipment bonus key or an MIO's equipment types match,
    /// exactly (case-sensitive): the hull, its archetype, the hull's equipment type keywords (<see cref="EquipmentKeywords"/>)
    /// and the equipment groups holding any of them. The game matches the variant's hull equipment, so the modules a
    /// design installs and its ship type (sub-unit) do not count.
    /// </summary>
    public List<string> EquipmentBonusTypes { get; }

    /// <summary>
    /// S-MIO-MATCH [RE] 0xc95730, 0xf8b020: the hull's type keywords, its archetype's <c>type</c> and its default modules'
    /// <c>add_equipment_type</c>, normalized: <c>capital_ship</c> drops <c>screen_ship</c>, <c>submarine</c> and
    /// <c>convoy</c>; else <c>submarine</c> drops <c>screen_ship</c> and <c>convoy</c>; else <c>screen_ship</c> drops
    /// <c>convoy</c>.
    /// </summary>
    public static IReadOnlyList<string> EquipmentKeywords(Hull hull)
    {
        var keywords = hull.DefaultModuleEquipmentTypes.Prepend(hull.EquipmentType)
            .Where(type => !string.IsNullOrEmpty(type))
            .Distinct(StringComparer.Ordinal)
            .ToList();
        string[] dropped = keywords.Contains(CapitalShip) ? [ScreenShip, Submarine, Convoy]
            : keywords.Contains(Submarine) ? [ScreenShip, Convoy]
            : keywords.Contains(ScreenShip) ? [Convoy]
            : [];
        return keywords.Except(dropped, StringComparer.Ordinal).ToList();
    }

    private const string CapitalShip = "capital_ship";
    private const string ScreenShip = "screen_ship";
    private const string Submarine = "submarine";
    private const string Convoy = "convoy";

    /// <summary>Design stats without country equipment bonuses, with the MIO's initial trait only.</summary>
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
    /// <paramref name="mioBonus"/> is the side's MIO bonus (<see cref="MioRules.EquipmentBonus"/>); null takes the MIO's
    /// initial trait only.
    /// </summary>
    public ShipStats ComputeStats(IReadOnlyList<ShipStats> equipmentBonuses, ShipStats? mioBonus = null)
    {
        var mio = mioBonus ?? MioRules.EquipmentBonus(this, Mio, null);
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

    private static List<CriticalPartCandidate> MergeCriticalParts(ShipType shipType, IEnumerable<StatModule> modules)
    {
        var candidates = new List<CriticalPartCandidate>();

        foreach (var part in shipType.CriticalParts.Concat(modules.SelectMany(module => module.CriticalParts)))
        {
            var index = candidates.FindIndex(candidate => candidate.Part == part);

            if (index < 0)
            {
                candidates.Add(new CriticalPartCandidate(part, part.BaseDamageInstance));
                continue;
            }

            var max = candidates[index].MaxInstances;

            if (max <= -1)
            {
                continue;
            }

            max += part.DamageInstancePerAddedModule;

            if (part.MaxDamageInstance > -1 && max > part.MaxDamageInstance)
            {
                max = part.MaxDamageInstance;
            }

            candidates[index] = candidates[index] with { MaxInstances = max };
        }

        return candidates;
    }
}
