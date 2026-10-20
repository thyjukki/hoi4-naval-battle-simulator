namespace NavySimulator.GameData.Contracts;

/// <summary>
/// Optional overrides for <c>NavySimulator.Engine.Battles.BattleRules</c>. Omitted values keep the engine defaults.
/// </summary>
public class BattleRulesDto
{
    public string? DamageRandomness { get; set; }
    public bool? NightEnabled { get; set; }
    public bool? WeatherEnabled { get; set; }
    public bool? NavalDominanceEnabled { get; set; }
    public bool? CarrierWingsExecuteNavalStrike { get; set; }
    public int? StartHourOfDay { get; set; }
    public int? AirRegionPhase { get; set; }
    public int? NightStartHour { get; set; }
    public int? NightEndHour { get; set; }
}
