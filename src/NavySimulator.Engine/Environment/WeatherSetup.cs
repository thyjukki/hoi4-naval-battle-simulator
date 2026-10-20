namespace NavySimulator.Engine.Environment;

/// <summary>
/// S-WEATHER: a scenario's weather. <see cref="WeatherMode.Fixed"/> keeps <see cref="Fixed"/> for the whole battle;
/// <see cref="WeatherMode.Simulated"/> draws a pattern from the region's weather periods with its own random stream (the
/// game's weather does not use the battle's dice), seeded by <see cref="Seed"/> or, when null, by the battle's seed.
/// </summary>
public sealed record WeatherSetup(WeatherMode Mode, RegionWeather Fixed = default, int? Seed = null)
{
    public static WeatherSetup Clear { get; } = new(WeatherMode.Fixed);
}
