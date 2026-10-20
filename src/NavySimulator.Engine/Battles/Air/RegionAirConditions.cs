using NavySimulator.Engine.Stats;

namespace NavySimulator.Engine.Battles.Air;

/// <summary>
/// A-DETECT: what the region's air detection reads from the location in an air hour: the mean darkness of the region's
/// provinces (0xe21640) and the region's weather holder (its <c>air_detection</c>). The default is daylight, no weather.
/// </summary>
public readonly record struct RegionAirConditions(double Darkness, ModifierSet? WeatherModifiers = null)
{
    public double WeatherModifier(string key)
    {
        return WeatherModifiers?.Get(key) ?? 0;
    }
}
