namespace NavySimulator.Setup.Contracts;

public class BattleScenarioDto
{
    public string ID { get; set; } = string.Empty;
    public string Terrain { get; set; } = string.Empty;
    public string Weather { get; set; } = string.Empty;
    public int MaxHours { get; set; }
    public int? Iterations { get; set; }
    public int? Seed { get; set; }
    public bool? ContinueAfterRetreat { get; set; }
    public bool? DontRetreat { get; set; }
    public BattleParticipantDto Attacker { get; set; } = new BattleParticipantDto();
    public BattleParticipantDto Defender { get; set; } = new BattleParticipantDto();
}


