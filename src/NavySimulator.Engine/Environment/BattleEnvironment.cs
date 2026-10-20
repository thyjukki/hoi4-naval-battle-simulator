using NavySimulator.Engine.Stats;

namespace NavySimulator.Engine.Environment;

/// <summary>
/// S-HOUR [RE] 0x15c5820: what the battle reads from its location each hour: the province's darkness (S-NIGHT,
/// combat+0xb8), the region's weather and its modifier holder (S-WEATHER, combat+0xa8 / +0xb0) and the region's naval
/// terrain (S-TERRAIN, combat+0x48). The default is no location: daylight, no weather, no terrain.
/// </summary>
public readonly record struct BattleEnvironment(
    double Darkness,
    NavalTerrain? Terrain = null,
    RegionWeather Weather = default,
    ModifierSet? WeatherModifiers = null)
{
    public static BattleEnvironment None => default;

    public bool IsDark => Darkness > 0;

    /// <summary>A key of the region's weather holder, whole (0 without weather).</summary>
    public double WeatherModifier(string key)
    {
        return WeatherModifiers?.Get(key) ?? 0;
    }

    /// <summary>
    /// S-POS-MOD, S-SCREEN [RE] 0x15c70b0: the location part of a side-level modifier read: the region's weather holder and
    /// the naval terrain's own keys, both whole.
    /// </summary>
    public double LocationModifier(string key)
    {
        return WeatherModifier(key) + (Terrain?.Modifiers.Get(key) ?? 0);
    }
}
