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

    /// <summary>The sub-unit's <c>categories</c> (e.g. <c>category_carrier_nav_bomber</c>), which doctrine bonuses name.</summary>
    public IReadOnlySet<string> SubUnitCategories;

    /// <summary>A-MIXED: the equipment's year, which orders the stockpile (newest first).</summary>
    public int Year;

    /// <summary>
    /// A-MIXED: the equipment's place in the equipment database (the import's order of <c>common/units/equipment</c>), which
    /// orders a wing's variants (0xbd1f20: the oldest type first, lost first).
    /// </summary>
    public int Order;

    public PlaneEquipment(
        string id,
        PlaneStats stats,
        IEnumerable<string>? types = null,
        IEnumerable<string>? missions = null,
        bool carrierCapable = false,
        string? subUnit = null,
        IEnumerable<string>? subUnitTypes = null,
        IEnumerable<string>? subUnitCategories = null,
        int year = 0,
        int order = 0)
    {
        ID = id;
        Stats = stats;
        Types = new HashSet<string>(types ?? [], StringComparer.Ordinal);
        Missions = new HashSet<string>(missions ?? [], StringComparer.Ordinal);
        CarrierCapable = carrierCapable;
        SubUnit = subUnit;
        SubUnitTypes = new HashSet<string>(subUnitTypes ?? [], StringComparer.Ordinal);
        SubUnitCategories = new HashSet<string>(subUnitCategories ?? [], StringComparer.Ordinal);
        Year = year;
        Order = order;
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
