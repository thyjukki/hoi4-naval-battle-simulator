namespace NavySimulator.GameData.Contracts;

/// <summary>
/// Optional overrides for <c>NavySimulator.Engine.Battles.BattleRules</c>. Omitted values keep the engine defaults.
/// </summary>
public class BattleRulesDto
{
    public string? FiringOrder { get; set; }
    public string? DamageRandomness { get; set; }
    public bool? ActivationDependsOnComposition { get; set; }
    public bool? FirstShotWaitsForCooldown { get; set; }
    public bool? NightEnabled { get; set; }
    public int? StartHourOfDay { get; set; }
    public bool? DailyManpowerRecoveryInBattle { get; set; }
}
