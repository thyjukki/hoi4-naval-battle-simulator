namespace NavySimulator.Engine.Results;

/// <summary>
/// Both sides' state for battle hour <see cref="Hour"/> (0-based), fought at <see cref="HourOfDay"/>, with the hour's
/// darkness (S-NIGHT) and the region's weather (S-WEATHER: the phenomenon key, e.g. <c>rain_light</c>; null when clear).
/// </summary>
public sealed record HourSummary(
    int Hour,
    int HourOfDay,
    bool Night,
    SideHourState Attacker,
    SideHourState Defender,
    double Darkness = 0,
    string? Weather = null);
