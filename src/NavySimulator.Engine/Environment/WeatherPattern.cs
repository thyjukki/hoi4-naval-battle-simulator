using NavySimulator.Engine.Battles.Mechanics;
using NavySimulator.Engine.Defines;
using NavySimulator.Engine.Random;

namespace NavySimulator.Engine.Environment;

/// <summary>
/// S-WEATHER-SIM [RE + ASSUMED]: a simulated weather pattern for one battle's region. The game visits each region's
/// weather state every <see cref="WeatherRules.VisitHours"/> hours (0xf18ad0) and rolls a new weather at the first visit
/// after the current one has run its duration (0xf20ef0). The game's hash depends on the whole run's random history, so
/// the pattern draws from its own stream: the start weather is one roll entered at a uniform point of its spell, and the
/// visit phase is uniform [ASSUMED]. Hours are asked in order; earlier hours are kept.
/// </summary>
public sealed class WeatherPattern
{
    private readonly SeaRegion region;
    private readonly GameDate battleStart;
    private readonly WeatherSettings settings;
    private readonly IRandomSource random;
    private readonly int visitHours;
    private readonly int phase;
    private readonly List<RegionWeather> hours = [];
    private RegionWeather current;
    private double nextChange;

    /// <param name="battleStart">The game date and hour of battle hour 0.</param>
    public WeatherPattern(SeaRegion region, GameDate battleStart, GameDefines defines, IRandomSource random)
    {
        this.region = region;
        this.battleStart = battleStart;
        settings = defines.Weather;
        this.random = random;
        visitHours = WeatherRules.VisitHours(region.WeatherStates, settings);
        phase = (int)(WeatherRules.Draw(random) * visitHours);

        if (WeatherRules.PeriodOf(region, battleStart) is { } period)
        {
            var (weather, duration) = WeatherRules.Roll(period, false, WeatherRules.Draw(random), WeatherRules.Draw(random), settings);
            current = weather;
            nextChange = WeatherRules.Draw(random) * duration;
        }
    }

    public int VisitHours => visitHours;

    /// <summary>The region's weather in battle hour <paramref name="hour"/> (0 or more).</summary>
    public RegionWeather At(int hour)
    {
        while (hours.Count <= hour)
        {
            var next = hours.Count;

            if (next >= nextChange && (next - phase) % visitHours == 0 && WeatherRules.PeriodOf(region, battleStart.AddHours(next)) is { } period)
            {
                var (weather, duration) = WeatherRules.Roll(period, current.ArcticWater, WeatherRules.Draw(random), WeatherRules.Draw(random), settings);
                current = weather;
                nextChange = next + duration;
            }

            hours.Add(current);
        }

        return hours[Math.Max(0, hour)];
    }
}
