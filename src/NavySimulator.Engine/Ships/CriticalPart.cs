using NavySimulator.Engine.Stats;

namespace NavySimulator.Engine;

/// <summary>
/// S-CRIT-PART: a part a critical hit can damage (<c>common/units/critical_parts</c>). On the hit that damages it, its
/// damage multipliers and additions replace the critical multiplier; afterwards its stat penalties
/// (<c>stat × max(0, 1 + count × penalty)</c>, S-STATS-PARTS) and modifiers (× count × (1 + naval_critical_effect_factor))
/// stay on the ship. <see cref="BaseDamageInstance"/>, <see cref="DamageInstancePerAddedModule"/> and
/// <see cref="MaxDamageInstance"/> decide how many times it can be damaged (-1: any).
/// </summary>
public sealed class CriticalPart(
    string id,
    double chance,
    double strDamageMultiplier,
    double orgDamageMultiplier,
    double strDamage,
    double orgDamage,
    int baseDamageInstance,
    int damageInstancePerAddedModule,
    int maxDamageInstance,
    ShipStats statPenalties,
    ModifierSet modifiers)
{
    public string ID { get; } = id;

    /// <summary>The part's weight among the ship's candidates.</summary>
    public double Chance { get; } = chance;

    public double StrDamageMultiplier { get; } = strDamageMultiplier;
    public double OrgDamageMultiplier { get; } = orgDamageMultiplier;
    public double StrDamage { get; } = strDamage;
    public double OrgDamage { get; } = orgDamage;
    public int BaseDamageInstance { get; } = baseDamageInstance;
    public int DamageInstancePerAddedModule { get; } = damageInstancePerAddedModule;
    public int MaxDamageInstance { get; } = maxDamageInstance;

    /// <summary>Per stat, the factor penalty of one damage instance.</summary>
    public ShipStats StatPenalties { get; } = statPenalties;

    /// <summary>Modifiers of one damage instance, by game key.</summary>
    public ModifierSet Modifiers { get; } = modifiers;
}
