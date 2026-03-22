namespace NavySimulator.Setup.Contracts;

public class BattleParticipantDto
{
    public string FleetID { get; set; } = string.Empty;
    public string Commander { get; set; } = string.Empty;
    public string Doctrine { get; set; } = string.Empty;
    public int ExternalNavalStrikePlanes { get; set; }
    public List<string> ResearchIDs { get; set; } = [];
    public List<string> SpiritIDs { get; set; } = [];
}


