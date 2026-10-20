using NavySimulator.Engine.Environment;

namespace NavySimulator.Engine.Defines;

/// <summary>
/// S-WEATHER-SIM [PDX + RE]: the <c>settings</c> of <c>common/weather.txt</c> the region weather roll reads (0xf20ef0,
/// 0xf15c50, 0xf18ad0). Defaults are vanilla 1.19.3 (TFB has no weather.txt of its own).
/// </summary>
public sealed class WeatherSettings
{
    /// <summary><c>duration</c>: how long a new weather lasts, in days, between the two values.</summary>
    public Dictionary<Weather, (double Min, double Max)> Duration { get; } = new()
    {
        [Weather.None] = (5, 12),
        [Weather.RainLight] = (5, 20),
        [Weather.RainHeavy] = (2, 15),
        [Weather.Snow] = (5, 20),
        [Weather.Blizzard] = (2, 15),
        [Weather.Sandstorm] = (2, 10)
    };

    /// <summary>
    /// <c>snowing.temperature = { 1.0 -4.0 }</c>: below the first value no rain, and snow and blizzard grow to their full
    /// weight at the second.
    /// </summary>
    public double SnowingTemperatureNoRain { get; set; } = 1.0;

    public double SnowingTemperatureFull { get; set; } = -4.0;

    /// <summary><c>arctic_water.arctic_water_temperature</c>: the ramp a region without arctic water starts it on.</summary>
    public (double Min, double Max) ArcticWaterTemperature { get; set; } = (-5.0, 0.0);

    /// <summary><c>arctic_water.arctic_water_end_temperature</c>: the ramp a region with arctic water keeps it on.</summary>
    public (double Min, double Max) ArcticWaterEndTemperature { get; set; } = (1.0, 5.0);

    /// <summary><c>performance.regions_per_update</c>: region weather states visited per hour, in turn.</summary>
    public int RegionsPerUpdate { get; set; } = 7;
}
