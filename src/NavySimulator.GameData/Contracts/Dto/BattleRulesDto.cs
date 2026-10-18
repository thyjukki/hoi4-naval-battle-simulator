namespace NavySimulator.GameData.Contracts;

/// <summary>
/// Optional overrides for <c>NavySimulator.Engine.Battles.BattleRules</c>. Omitted values keep the engine defaults.
/// </summary>
public class BattleRulesDto
{
    public string? DamageRandomness { get; set; }
    public bool? NightEnabled { get; set; }
    public int? StartHourOfDay { get; set; }
}
