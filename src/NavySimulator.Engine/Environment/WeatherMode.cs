namespace NavySimulator.Engine.Environment;

/// <summary>S-WEATHER: where a battle's weather comes from.</summary>
public enum WeatherMode
{
    /// <summary>One weather for the whole battle (<see cref="WeatherSetup.Fixed"/>; clear by default).</summary>
    Fixed,

    /// <summary>A pattern drawn from the battle region's weather periods for the battle's dates (S-WEATHER-SIM).</summary>
    Simulated
}
