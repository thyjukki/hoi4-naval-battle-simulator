namespace NavySimulator.GameData.Contracts;

/// <summary>
/// S-WEATHER-REGION [PDX]: one <c>period</c> of a strategic region's <c>weather</c> block. Days and months are the script's,
/// both counted from 0 (<c>between = { 0.4 30.4 }</c> is 1 to 31 May). The phenomenon values are the script's weights.
/// </summary>
public class WeatherPeriodDto
{
    public int FromDay { get; set; }
    public int FromMonth { get; set; }
    public int ToDay { get; set; }
    public int ToMonth { get; set; }
    public double MinTemperature { get; set; }
    public double MaxTemperature { get; set; }
    public double NoPhenomenon { get; set; }
    public double RainLight { get; set; }
    public double RainHeavy { get; set; }
    public double Snow { get; set; }
    public double Blizzard { get; set; }
    public double ArcticWater { get; set; }
    public double Mud { get; set; }
    public double Sandstorm { get; set; }
    public double MinSnowLevel { get; set; }
}
