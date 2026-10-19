namespace NavySimulator.GameData.Contracts;

/// <summary>
/// A critical part of <c>common/units/critical_parts</c>: what a critical hit can damage on a ship (S-CRIT-PART).
/// Defaults are the game's for keys a part leaves out (constructor 0x1019290).
/// </summary>
public class CriticalPartDto
{
    public string ID { get; set; } = string.Empty;

    /// <summary>The game's name (localisation), e.g. "Damaged Heavy Guns".</summary>
    public string? Name { get; set; }

    /// <summary><c>chance</c>: the part's weight among a ship's candidates.</summary>
    public double Chance { get; set; } = 1;

    /// <summary><c>str_damage_multiplier</c>: replaces the critical multiplier on the hit's HP damage.</summary>
    public double StrDamageMultiplier { get; set; } = 1;

    /// <summary><c>org_damage_multiplier</c>: replaces the critical multiplier on the hit's organization damage.</summary>
    public double OrgDamageMultiplier { get; set; } = 1;

    /// <summary><c>str_damage</c>: HP damage added to the hit.</summary>
    public double StrDamage { get; set; }

    /// <summary><c>org_damage</c>: organization damage added to the hit.</summary>
    public double OrgDamage { get; set; }

    /// <summary><c>base_damage_instance</c>: times the part can be damaged once a sub-unit or module brings it (-1: any).</summary>
    public int BaseDamageInstance { get; set; } = 1;

    /// <summary><c>damage_instance_per_added_module</c>: more times for each further module that brings it.</summary>
    public int DamageInstancePerAddedModule { get; set; }

    /// <summary><c>max_damage_instance</c>: cap on the times (-1: none).</summary>
    public int MaxDamageInstance { get; set; } = -1;

    /// <summary><c>stat_penalties</c>: factors on the ship's stats, <c>stat × max(0, 1 + count × penalty)</c> (S-STATS-PARTS).</summary>
    public ShipStatsDto StatPenalties { get; set; } = new();

    /// <summary><c>modifier</c>: modifiers of the ship by game key, × count × (1 + naval_critical_effect_factor).</summary>
    public Dictionary<string, double> Modifiers { get; set; } = [];
}
