namespace NavySimulator.Engine.Environment;

/// <summary>
/// S-WEATHER [RE] 0xf220d0: the weather phenomenon of the battle's strategic region. Rain light, rain heavy, snow and
/// blizzard come from one roll and exclude each other; sandstorm only without them. Arctic water is separate
/// (<see cref="RegionWeather.ArcticWater"/>).
/// </summary>
public enum Weather
{
    None,
    RainLight,
    RainHeavy,
    Snow,
    Blizzard,
    Sandstorm
}
