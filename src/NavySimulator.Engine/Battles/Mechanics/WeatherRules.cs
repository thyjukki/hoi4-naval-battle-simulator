using NavySimulator.Engine.Defines;
using NavySimulator.Engine.Environment;
using NavySimulator.Engine.Random;

namespace NavySimulator.Engine.Battles.Mechanics;

/// <summary>
/// S-WEATHER-SIM [RE] 0xf20ef0, 0xf191f0, 0xf15c50: a region's weather roll: the period of the date, the phenomenon by
/// the period's weights (snow only when cold), arctic water, and how long the new weather lasts. Spec:
/// docs/mechanics/surface.md.
/// </summary>
public static class WeatherRules
{
    /// <summary>[RE] 0xf191f0: the first of the region's periods whose <c>between</c> holds the date; no interpolation.</summary>
    public static WeatherPeriod? PeriodOf(SeaRegion region, GameDate date)
    {
        var month = date.Month - 1;
        var day = date.Day - 1;
        var key = month * 100 + day;
        return region.Weather.FirstOrDefault(period =>
            key >= period.FromMonth * 100 + period.FromDay && key <= period.ToMonth * 100 + period.ToDay);
    }

    /// <summary>
    /// [RE + ASSUMED] The region's temperature the roll reads is the mean of its weather cells' temperatures (state+0x138),
    /// whose walk is not decoded; the middle of the period's range stands in.
    /// </summary>
    public static double Temperature(WeatherPeriod period)
    {
        return (period.MinTemperature + period.MaxTemperature) / 2;
    }

    /// <summary>
    /// [RE] 0xf20ef0: the weather of one roll and its duration in hours. With <c>t = (clamp(T, full, noRain) − full) /
    /// (noRain − full)</c> snow and blizzard are × (1 − t), and below <c>noRain</c> there is no rain; then
    /// <c>x = U1 × (no_phenomenon + rain_light + rain_heavy + snow + blizzard)</c> picks the first cumulative weight at or
    /// above x in that order, else no phenomenon. Mud and sandstorm do not count (sandstorm needs land provinces). Arctic
    /// water when <c>U1 &lt; arctic_water × clamp(1 − (T − a) / (b − a), 0, 1)</c>, on the start ramp without it, the end ramp
    /// with it [RE, medium: the ramp's direction]. The duration is <c>24 × (min + min(U1, U2) × (max − min))</c> hours of
    /// the new weather's <c>settings.duration</c> (days).
    /// </summary>
    public static (RegionWeather Weather, double DurationHours) Roll(WeatherPeriod period, bool arcticWater, double u1, double u2, WeatherSettings settings)
    {
        var temperature = Temperature(period);
        var weights = Weights(period, settings);
        var x = u1 * weights.Sum(entry => entry.Weight);
        var phenomenon = Weather.None;
        var cumulative = 0.0;

        foreach (var (weather, weight) in weights.Where(entry => entry.Weather != Weather.None))
        {
            cumulative += weight;

            if (weight > 0 && x <= cumulative)
            {
                phenomenon = weather;
                break;
            }
        }

        var (a, b) = arcticWater ? settings.ArcticWaterEndTemperature : settings.ArcticWaterTemperature;
        var ramp = b == a ? (temperature <= a ? 1 : 0) : Math.Clamp(1 - (temperature - a) / (b - a), 0, 1);
        var arctic = u1 < period.ArcticWater * ramp;
        var (min, max) = settings.Duration.GetValueOrDefault(phenomenon, (0, 0));
        var duration = 24 * (min + Math.Min(u1, u2) * (max - min));
        return (new RegionWeather(phenomenon, arctic), duration);
    }

    /// <summary>
    /// [RE] 0xf20ef0: the roll's weights in its order (rain light, rain heavy, snow, blizzard) and no phenomenon last, after
    /// the temperature rule: below <c>snowing.temperature</c>'s first value no rain; snow and blizzard grow to their full
    /// weight at the second.
    /// </summary>
    public static IReadOnlyList<(Weather Weather, double Weight)> Weights(WeatherPeriod period, WeatherSettings settings)
    {
        var temperature = Temperature(period);
        var noRain = settings.SnowingTemperatureNoRain;
        var full = settings.SnowingTemperatureFull;
        var warmth = noRain == full ? (temperature >= noRain ? 1 : 0) : (Math.Clamp(temperature, full, noRain) - full) / (noRain - full);
        var rain = temperature < noRain ? 0 : 1;
        return
        [
            (Weather.RainLight, period.RainLight * rain), (Weather.RainHeavy, period.RainHeavy * rain),
            (Weather.Snow, period.Snow * (1 - warmth)), (Weather.Blizzard, period.Blizzard * (1 - warmth)), (Weather.None, period.NoPhenomenon)
        ];
    }

    /// <summary>The chance of each weather at a roll: its weight over the sum.</summary>
    public static IReadOnlyList<(Weather Weather, double Chance)> Chances(WeatherPeriod period, WeatherSettings settings)
    {
        var weights = Weights(period, settings);
        var total = weights.Sum(entry => entry.Weight);
        return total <= 0 ? [(Weather.None, 1.0)] : weights.Select(entry => (entry.Weather, entry.Weight / total)).ToList();
    }

    /// <summary>
    /// [RE] 0xf18ad0: the hours between two visits of one region's weather state: the states (strategic regions, by the
    /// highest id + 1) visited <c>regions_per_update</c> an hour, in turn (TFB: 315 / 7 = 45).
    /// </summary>
    public static int VisitHours(int weatherStates, WeatherSettings settings)
    {
        return Math.Max(1, (int)Math.Ceiling(weatherStates / (double)Math.Max(1, settings.RegionsPerUpdate)));
    }

    /// <summary>A uniform draw of the simulated pattern's own stream.</summary>
    public static double Draw(IRandomSource random)
    {
        return random.NextDouble(RollKind.Weather);
    }
}
