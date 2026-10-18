namespace NavySimulator.GameData.Contracts;

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

    /// <summary>
    /// Define sources applied on top of the built-in vanilla defines, in order: <c>.lua</c> files or game/mod root
    /// directories (their <c>common/defines/*.lua</c>). Relative paths resolve against the data directory.
    /// </summary>
    public List<string> Defines { get; set; } = [];

    public BattleRulesDto? Rules { get; set; }
    public BattleParticipantDto Attacker { get; set; } = new BattleParticipantDto();
    public BattleParticipantDto Defender { get; set; } = new BattleParticipantDto();
}


