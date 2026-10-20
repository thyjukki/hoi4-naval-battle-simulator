namespace NavySimulator.Engine.Environment;

/// <summary>
/// S-WEATHER-REGION [PDX]: one period of a strategic region's <c>weather</c> block. Days and months count from 0, as the
/// script's <c>between = { 0.4 30.4 }</c> (1 to 31 May). The phenomenon values are the script's weights.
/// </summary>
public sealed record WeatherPeriod(
    int FromDay,
    int FromMonth,
    int ToDay,
    int ToMonth,
    double MinTemperature,
    double MaxTemperature,
    double NoPhenomenon,
    double RainLight,
    double RainHeavy,
    double Snow,
    double Blizzard,
    double ArcticWater,
    double Sandstorm);
