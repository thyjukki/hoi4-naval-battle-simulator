namespace NavySimulator.GameData.Contracts;

/// <summary>A plane equipment of the game (A-PLANES), as <c>import-planes</c> writes it to <c>planes/</c>.</summary>
public class PlaneDto
{
    public string ID { get; set; } = string.Empty;
    public string? Name { get; set; }

    /// <summary>The archetype (e.g. <c>cv_nav_bomber_equipment</c>), which the plane's stats, types and missions default to.</summary>
    public string? Archetype { get; set; }

    public string? ArchetypeName { get; set; }
    public int? Year { get; set; }

    /// <summary>Equipment <c>type</c> keywords, e.g. <c>naval_bomber</c>, <c>fighter</c> (A-WINGS).</summary>
    public List<string> Types { get; set; } = [];

    /// <summary><c>allow_mission_type</c>: the missions the plane can fly, e.g. <c>naval_bomber</c>, <c>port_strike</c>.</summary>
    public List<string> Missions { get; set; } = [];

    public bool CarrierCapable { get; set; }

    /// <summary>The air sub-unit (<c>common/units/air.txt</c>) that needs the plane's archetype, e.g. <c>cv_nav_bomber</c>.</summary>
    public string? SubUnit { get; set; }

    /// <summary>The sub-unit's <c>type</c> keywords; the game's fighter tests read these, not the equipment's (A-WINGS).</summary>
    public List<string> SubUnitTypes { get; set; } = [];

    /// <summary>The sub-unit's <c>carrier_air_wing_size</c>: planes per wing on a carrier.</summary>
    public int? CarrierWingSize { get; set; }

    /// <summary>The sub-unit's <c>land_air_wing_size</c>: planes per wing on a land air base.</summary>
    public int? LandWingSize { get; set; }
    public PlaneStatsDto Stats { get; set; } = new();
}
