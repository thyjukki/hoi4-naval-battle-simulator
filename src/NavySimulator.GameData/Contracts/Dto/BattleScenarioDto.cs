namespace NavySimulator.GameData.Contracts;

public class BattleScenarioDto
{
    public string ID { get; set; } = string.Empty;

    /// <summary>
    /// S-TERRAIN: the naval terrain (<c>naval-terrains/</c> ID, e.g. <c>water_fjords</c>); empty or <c>region</c>: the
    /// region's own (<c>ocean</c> when it sets none, or no region).
    /// </summary>
    public string Terrain { get; set; } = string.Empty;

    /// <summary>
    /// S-WEATHER: <c>clear</c> (or empty), a phenomenon for the whole battle (<c>rain_light</c>, <c>rain_heavy</c>,
    /// <c>snow</c>, <c>blizzard</c>, <c>sandstorm</c>), or <c>simulated</c>: a pattern from the region's weather periods.
    /// </summary>
    public string Weather { get; set; } = string.Empty;

    /// <summary>S-WEATHER: arctic water on top of a fixed weather (the simulated pattern decides it itself).</summary>
    public bool? ArcticWater { get; set; }

    /// <summary>S-WEATHER-SIM: the simulated pattern's own seed; null: the battle's seed.</summary>
    public int? WeatherSeed { get; set; }

    /// <summary>S-REGION: the strategic region (<c>sea-regions/</c> ID); null: none (no region weather, no terrain from it).</summary>
    public int? RegionID { get; set; }

    /// <summary>S-NIGHT: the battle province, a sea province of the region; null: the region's first province.</summary>
    public int? ProvinceID { get; set; }

    /// <summary>
    /// S-NIGHT, S-WEATHER-SIM: the day the battle starts, as the game writes dates (<c>1936.5.6</c>); the hour is
    /// <c>rules.startHourOfDay</c>. With a region and this, darkness follows the province and the date; without, the
    /// rules' night window.
    /// </summary>
    public string? StartDate { get; set; }
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


