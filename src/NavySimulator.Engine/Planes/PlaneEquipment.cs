using NavySimulator.Engine.Defines;

namespace NavySimulator.Engine;

/// <summary>
/// A plane equipment of the game (e.g. <c>cv_nav_bomber_equipment_2</c>) with its stats, equipment types and allowed
/// missions [PDX]; planes have no designer here (A-PLANES).
/// </summary>
public class PlaneEquipment
{
    public string ID;
    public PlaneStats Stats;

    /// <summary>The equipment's <c>type</c> keywords (its archetype's when it has none), e.g. <c>naval_bomber</c>, <c>fighter</c>.</summary>
    public IReadOnlySet<string> Types;

    /// <summary>The missions in <c>allow_mission_type</c> (the archetype's), e.g. <c>naval_bomber</c>, <c>port_strike</c>.</summary>
    public IReadOnlySet<string> Missions;

    /// <summary><c>carrier_capable</c>: the plane can fly from a carrier.</summary>
    public bool CarrierCapable;

    /// <summary>The air sub-unit that flies the plane (e.g. <c>cv_nav_bomber</c>): the game pools wings by it; null: unknown.</summary>
    public string? SubUnit;

    /// <summary>The sub-unit's <c>type</c> keywords, which the fighter tests read (A-WINGS-TYPES).</summary>
    public IReadOnlySet<string> SubUnitTypes;

    public PlaneEquipment(
        string id,
        PlaneStats stats,
        IEnumerable<string>? types = null,
        IEnumerable<string>? missions = null,
        bool carrierCapable = false,
        string? subUnit = null,
        IEnumerable<string>? subUnitTypes = null)
    {
        ID = id;
        Stats = stats;
        Types = new HashSet<string>(types ?? [], StringComparer.Ordinal);
        Missions = new HashSet<string>(missions ?? [], StringComparer.Ordinal);
        CarrierCapable = carrierCapable;
        SubUnit = subUnit;
        SubUnitTypes = new HashSet<string>(subUnitTypes ?? [], StringComparer.Ordinal);
    }

    /// <summary>The wing pool the plane's wings join on a base: its sub-unit, else the plane itself.</summary>
    public string Pool => SubUnit ?? ID;

    // [ASSUMED] Planes without a known sub-unit use their equipment types.
    private IReadOnlySet<string> FighterTestTypes => SubUnitTypes.Count > 0 ? SubUnitTypes : Types;

    /// <summary>A-WINGS-TYPES: a fighter-type plane (by its sub-unit), which defends against strikes (A-INTERCEPT).</summary>
    public bool IsFighterType(HardcodedValues values)
    {
        return FighterTestTypes.Overlaps(values.FighterSubUnitTypes);
    }

    /// <summary>A-WINGS-TYPES: fighter-type for the sortie bonus of an air object whose first wing is one (A-SORTIE).</summary>
    public bool IsSortieFighterType(HardcodedValues values)
    {
        return FighterTestTypes.Overlaps(values.SortieFighterSubUnitTypes);
    }

    /// <summary>A-WINGS-STRIKE: a plane whose wings are in the strike list (naval strike, kamikaze or port strike).</summary>
    public bool IsInStrikeList(HardcodedValues values)
    {
        return Missions.Overlaps(values.StrikeListMissions);
    }

    /// <summary>A-WINGS-STRIKE: a plane that can fly naval strikes, so its wings can deal damage at sea.</summary>
    public bool IsNavalStriker(HardcodedValues values)
    {
        return Missions.Overlaps(values.NavalStrikeMissions);
    }
}
