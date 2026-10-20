using NavySimulator.Engine.Defines;
using NavySimulator.Engine.Stats;

namespace NavySimulator.Engine.Environment;

/// <summary>
/// S-WEATHER [RE] 0xf220d0: the weather of the battle's strategic region: one phenomenon and, independently, arctic water.
/// The region's modifier holder is the static block of each (<c>weather_rain_light</c>, ..., <c>weather_arctic_water</c>),
/// added whole.
/// </summary>
public readonly record struct RegionWeather(Weather Phenomenon, bool ArcticWater = false)
{
    public static RegionWeather Clear => default;

    /// <summary>
    /// [RE] 0x15c5820: the battle's weather type (combat+0xa8), as the in-game log records it: 2 rain light, 3 rain heavy,
    /// 4 snow, 5 blizzard, 0 otherwise (sandstorm and arctic water included).
    /// </summary>
    public int CombatType => Phenomenon switch
    {
        Weather.RainLight => 2,
        Weather.RainHeavy => 3,
        Weather.Snow => 4,
        Weather.Blizzard => 5,
        _ => 0
    };

    /// <summary>The static modifier blocks in the region's holder.</summary>
    public IEnumerable<string> Blocks()
    {
        if (BlockOf(Phenomenon) is { } block)
        {
            yield return block;
        }

        if (ArcticWater)
        {
            yield return StaticModifiers.WeatherArcticWater;
        }
    }

    /// <summary>The region's modifier holder (region state +0x48): its blocks, whole.</summary>
    public ModifierSet Modifiers(StaticModifiers modifiers)
    {
        return new ModifierSet(Blocks().SelectMany(block => modifiers.Blocks.GetValueOrDefault(block) ?? []));
    }

    /// <summary>The weather of a log's combat type (S-REPLAY): sandstorm and arctic water are not told apart from clear.</summary>
    public static RegionWeather FromCombatType(int type)
    {
        return new RegionWeather(type switch
        {
            2 => Weather.RainLight,
            3 => Weather.RainHeavy,
            4 => Weather.Snow,
            5 => Weather.Blizzard,
            _ => Weather.None
        });
    }

    public static string? BlockOf(Weather weather)
    {
        return weather switch
        {
            Weather.RainLight => StaticModifiers.WeatherRainLight,
            Weather.RainHeavy => StaticModifiers.WeatherRainHeavy,
            Weather.Snow => StaticModifiers.WeatherSnow,
            Weather.Blizzard => StaticModifiers.WeatherBlizzard,
            Weather.Sandstorm => StaticModifiers.WeatherSandstorm,
            _ => null
        };
    }

    /// <summary>The script's phenomenon key (<c>rain_light</c>, ...), as scenarios and region files name it; null for none.</summary>
    public static string? KeyOf(Weather weather)
    {
        return weather switch
        {
            Weather.RainLight => "rain_light",
            Weather.RainHeavy => "rain_heavy",
            Weather.Snow => "snow",
            Weather.Blizzard => "blizzard",
            Weather.Sandstorm => "sandstorm",
            _ => null
        };
    }

    public override string ToString()
    {
        var phenomenon = KeyOf(Phenomenon) ?? "no phenomenon";
        return ArcticWater ? $"{phenomenon} + arctic_water" : phenomenon;
    }
}
