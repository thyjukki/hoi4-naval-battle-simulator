namespace NavySimulator.Engine.Environment;

/// <summary>
/// S-REGION: a strategic region with sea provinces (<c>map/strategicregions</c>): its naval terrain (S-TERRAIN), weather
/// periods (S-WEATHER-REGION) and provinces with their map positions (S-NIGHT).
/// </summary>
public sealed class SeaRegion(
    int id,
    string name,
    NavalTerrain? navalTerrain,
    IReadOnlyList<SeaProvince> provinces,
    IReadOnlyList<WeatherPeriod> weather,
    int mapWidth,
    int mapHeight,
    int weatherStates)
{
    public int ID { get; } = id;

    public string Name { get; } = name;

    /// <summary>The region's naval terrain; null when the region sets none and the data has no <c>ocean</c> terrain.</summary>
    public NavalTerrain? NavalTerrain { get; } = navalTerrain;

    public IReadOnlyList<SeaProvince> Provinces { get; } = provinces;

    public IReadOnlyList<WeatherPeriod> Weather { get; } = weather;

    public int MapWidth { get; } = mapWidth;

    public int MapHeight { get; } = mapHeight;

    /// <summary>S-WEATHER-SIM: how many region weather states the game keeps (the highest strategic region id + 1), which sets how often each is visited.</summary>
    public int WeatherStates { get; } = weatherStates;
}
