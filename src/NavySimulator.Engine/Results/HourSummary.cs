namespace NavySimulator.Engine.Results;

/// <summary>Both sides' state for battle hour <see cref="Hour"/> (0-based), fought at <see cref="HourOfDay"/>.</summary>
public sealed record HourSummary(
    int Hour,
    int HourOfDay,
    bool Night,
    SideHourState Attacker,
    SideHourState Defender);
