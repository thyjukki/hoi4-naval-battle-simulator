namespace NavySimulator.Engine;

/// <summary>
/// A naval sub-unit of <c>common/units</c> (light cruiser, battleship, ...). A design's ship type is picked from its hull
/// and modules (S-DESIGN-TYPE); the type gives the ship its combat role and its organization (S-STATS-SHIP).
/// </summary>
public class ShipType(
    string id,
    ShipRole role,
    IReadOnlyList<string> unitTypes,
    int priority,
    IReadOnlyList<string> hulls,
    IReadOnlyList<string> requiredModules,
    double organization)
{
    public string ID { get; } = id;
    public ShipRole Role { get; } = role;

    /// <summary>The sub-unit's <c>type</c> block, e.g. <c>screen_ship</c>.</summary>
    public IReadOnlyList<string> UnitTypes { get; } = unitTypes;

    public int Priority { get; } = priority;

    /// <summary>Hull archetypes (or hulls) named in <c>need</c> / <c>need_equipment</c>.</summary>
    public IReadOnlyList<string> Hulls { get; } = hulls;

    /// <summary><c>need_equipment_modules = { any = { ... } }</c>: the design needs at least one of these modules. Empty: no condition.</summary>
    public IReadOnlyList<string> RequiredModules { get; } = requiredModules;

    /// <summary><c>max_organisation</c>.</summary>
    public double Organization { get; } = organization;

    public bool Needs(Hull hull)
    {
        return Hulls.Contains(hull.ID, StringComparer.Ordinal) || Hulls.Contains(hull.Archetype, StringComparer.Ordinal);
    }
}
