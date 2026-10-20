using NavySimulator.Engine.Stats;

namespace NavySimulator.Engine.Doctrines;

/// <summary>
/// S-DOCTRINE-EFFECT: what a grand doctrine, milestone, subdoctrine, reward or spirit gives its country. Spec:
/// docs/mechanics/surface.md.
/// </summary>
public sealed class CountryEffects(
    ModifierSet countryModifiers,
    IReadOnlyList<Research> statBonuses,
    IReadOnlyList<string> notSimulated,
    IReadOnlyList<PlaneBonus>? planeBonuses = null)
{
    public static CountryEffects Empty { get; } = new(ModifierSet.Empty, [], []);

    /// <summary>S-STATS-COUNTRY: country modifiers by game key; every ship of the side gets them.</summary>
    public ModifierSet CountryModifiers { get; } = countryModifiers;

    /// <summary>
    /// The sub-unit bonus blocks (technology-like, S-STATS-SHIP) and <c>equipment_bonus</c> blocks (equipment bonuses,
    /// S-STATS-BONUS), each as a <see cref="Research"/> scoped to its sub-unit or equipment type.
    /// </summary>
    public IReadOnlyList<Research> StatBonuses { get; } = statBonuses;

    /// <summary>The sub-unit bonus blocks with plane stats, for the plane sub-units or categories they name (A-STATS).</summary>
    public IReadOnlyList<PlaneBonus> PlaneBonuses { get; } = planeBonuses ?? [];

    /// <summary>Parts the simulator has no counterpart for (stats it does not model, scripted effects), as text.</summary>
    public IReadOnlyList<string> NotSimulated { get; } = notSimulated;
}
