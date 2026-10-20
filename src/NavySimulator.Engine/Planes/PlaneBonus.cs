using NavySimulator.Engine.Stats;

namespace NavySimulator.Engine;

/// <summary>
/// S-DOCTRINE-EFFECT / A-STATS: a sub-unit bonus block of a doctrine or spirit that names a plane sub-unit or category
/// (e.g. <c>category_carrier_nav_bomber = { naval_strike_attack = 0.1 }</c>), with its plane stat keys.
/// </summary>
public sealed record PlaneBonus(string Source, string Target, ModifierSet Values)
{
    /// <summary>The plane stat keys a bonus block can carry that the simulator has a plane stat for (A-STATS).</summary>
    public static IReadOnlySet<string> StatKeys { get; } = new HashSet<string>(
        ["air_attack", "air_defence", "air_agility", "maximum_speed", "naval_strike_attack", "naval_strike_targetting", "air_range", "reliability"],
        StringComparer.Ordinal);

    /// <summary>Whether the block names the plane's sub-unit or one of its categories.</summary>
    public bool AppliesTo(PlaneEquipment plane)
    {
        return Target == plane.SubUnit || plane.SubUnitCategories.Contains(Target);
    }
}
