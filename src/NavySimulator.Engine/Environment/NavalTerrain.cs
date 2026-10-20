using NavySimulator.Engine.Leaders;
using NavySimulator.Engine.Stats;

namespace NavySimulator.Engine.Environment;

/// <summary>
/// S-TERRAIN: a naval terrain of <c>common/terrain</c> (<c>naval_terrain = yes</c>), the battle region's
/// <c>naval_terrain</c>. Spec: docs/mechanics/surface.md.
/// </summary>
public sealed class NavalTerrain(
    string id,
    ModifierSet modifiers,
    IReadOnlyDictionary<string, ModifierSet> subUnitModifiers,
    IReadOnlyDictionary<string, LeaderAdjuster> subUnitAdjusters)
{
    public string ID { get; } = id;

    /// <summary>The terrain's own modifiers (e.g. <c>positioning</c>, <c>navy_visibility</c>).</summary>
    public ModifierSet Modifiers { get; } = modifiers;

    /// <summary>The modifiers of each sub-unit block, by the ship type's sub-unit (e.g. <c>submarine</c>).</summary>
    public IReadOnlyDictionary<string, ModifierSet> SubUnitModifiers { get; } = subUnitModifiers;

    /// <summary>The <c>units = { attack defence movement }</c> adjusters of each sub-unit block.</summary>
    public IReadOnlyDictionary<string, LeaderAdjuster> SubUnitAdjusters { get; } = subUnitAdjusters;

    public ModifierSet SubUnitModifier(string subUnit)
    {
        return SubUnitModifiers.GetValueOrDefault(subUnit) ?? ModifierSet.Empty;
    }

    public LeaderAdjuster SubUnitAdjuster(string subUnit)
    {
        return SubUnitAdjusters.GetValueOrDefault(subUnit);
    }
}
