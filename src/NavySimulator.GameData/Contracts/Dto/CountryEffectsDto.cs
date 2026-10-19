namespace NavySimulator.GameData.Contracts;

/// <summary>
/// S-DOCTRINE-EFFECT: what a grand doctrine, milestone, subdoctrine, reward or spirit gives its country, with the game's
/// script keys (the "activation effects" of <c>common/doctrines/_documentation.md</c>).
/// </summary>
public class CountryEffectsDto
{
    /// <summary>S-STATS-COUNTRY: top-level keys and <c>modifier</c>/<c>modifiers</c> blocks, e.g. <c>naval_retreat_speed</c>.</summary>
    public Dictionary<string, double> CountryModifiers { get; set; } = [];

    /// <summary>
    /// S-STATS-SHIP: sub-unit bonuses by sub-unit or sub-unit category, then by stat key, e.g.
    /// <c>battleship → hg_attack 0.05</c>; they reach ships like a technology's.
    /// </summary>
    public Dictionary<string, Dictionary<string, double>> UnitBonuses { get; set; } = [];

    /// <summary>
    /// S-STATS-BONUS: <c>equipment_bonus</c> blocks by equipment type, archetype or hull, then by stat key, e.g.
    /// <c>ship_hull_heavy → armor_value 0.075</c>.
    /// </summary>
    public Dictionary<string, Dictionary<string, double>> EquipmentBonuses { get; set; } = [];

    /// <summary>Script parts the simulator does not run, e.g. <c>effect</c> or <c>enable_tactic = tactic_x</c>.</summary>
    public List<string> Scripted { get; set; } = [];
}
