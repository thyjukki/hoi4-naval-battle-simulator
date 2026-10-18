namespace NavySimulator.GameData.Contracts;

public class BattleParticipantDto
{
    public string FleetID { get; set; } = string.Empty;
    public string Commander { get; set; } = string.Empty;
    public string Doctrine { get; set; } = string.Empty;
    public int? ShipExperienceLevel { get; set; }
    public int ExternalNavalStrikePlanes { get; set; }
    public List<string> ResearchIDs { get; set; } = [];
    public List<string> SpiritIDs { get; set; } = [];
    public string? EngagementLevel { get; set; }
    public string? ExternalPlaneID { get; set; }
    public double? ExternalMissionEfficiency { get; set; }
    public int? CarrierStanceIndex { get; set; }
    public double? CarrierNightTraffic { get; set; }
}


