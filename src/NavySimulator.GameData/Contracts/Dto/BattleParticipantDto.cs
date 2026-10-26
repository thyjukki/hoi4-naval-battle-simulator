namespace NavySimulator.GameData.Contracts;

public class BattleParticipantDto
{
    public string FleetID { get; set; } = string.Empty;

    /// <summary>S-MIO-COUNTRY: the country tag the side plays (e.g. <c>USA</c>); it decides which MIOs the side can have.</summary>
    public string? Country { get; set; }

    /// <summary>S-ADMIRAL: the admiral commanding the side's fleet; null: none.</summary>
    public AdmiralDto? Admiral { get; set; }

    /// <summary>
    /// S-POTF: the ship design whose first ship in the side's fleet is the country's pride of the fleet; null: none. The
    /// fleet's ships of one design are alike, so the design names the ship.
    /// </summary>
    public string? PrideOfTheFleet { get; set; }

    public int? ShipExperienceLevel { get; set; }
    public List<string> ResearchIDs { get; set; } = [];
    public List<string> SpiritIDs { get; set; } = [];

    /// <summary>S-DOCTRINE: the side's doctrines by folder ID (<c>naval</c>, <c>air</c>).</summary>
    public Dictionary<string, DoctrineSetupDto> Doctrines { get; set; } = [];

    /// <summary>S-MIO: the side's MIOs by MIO ID: unlocked traits and attached policy. MIOs left out have only their initial trait.</summary>
    public Dictionary<string, MioSetupDto> Mios { get; set; } = [];

    /// <summary>
    /// S-MIO: the MIO the side assigns to a design of its fleet, by design ID, instead of the design's own <c>mioID</c>;
    /// an empty string assigns none.
    /// </summary>
    public Dictionary<string, string> DesignMios { get; set; } = [];

    public string? Aggressiveness { get; set; }
    public string? RepairPriority { get; set; }

    /// <summary>A-EXTERNAL: the side's land-based planes, one entry per air base, in joining order.</summary>
    public List<LandBasedAirwingDto> LandBasedAirwings { get; set; } = [];

    /// <summary>A-REINFORCE: planes in the side's stockpile by plane ID; they refill its air wings during the battle.</summary>
    public Dictionary<string, int> PlaneStockpile { get; set; } = [];

    public int? CarrierStanceIndex { get; set; }
    public double? CarrierNightTraffic { get; set; }

    /// <summary>A-REGION-SYNC: the carrier wings fly a mission over the battle's region (null: the attacker yes, the defender no).</summary>
    public bool? CarrierWingsOnRegionMission { get; set; }

    /// <summary>A-REGION-SYNC: the carrier wings' supply ratio and fuel in their mission effectiveness (null: 1).</summary>
    public double? CarrierMissionEfficiency { get; set; }

    /// <summary>A-COVERAGE: the side's air bases (level, other planes there, supply); a land wing's base not listed has none of these.</summary>
    public List<AirBaseSetupDto> AirBases { get; set; } = [];

    /// <summary>A-DETECT-RAD: share of the battle region's provinces the side's radars cover, 0..1 (null: 0).</summary>
    public double? RadarCoverage { get; set; }

    /// <summary>
    /// S-DOMINANCE-DAILY: the side's current naval dominance in the battle's region at the start, as the save's region
    /// block has it (null: its ships' sum). Read with <c>rules.navalDominanceEnabled</c>.
    /// </summary>
    public double? NavalDominanceStart { get; set; }
}


