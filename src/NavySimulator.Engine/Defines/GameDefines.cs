namespace NavySimulator.Engine.Defines;

/// <summary>
/// All numeric inputs to the combat model for one run. Instances are independent, so tests and scenarios can
/// override values without affecting each other.
/// </summary>
public sealed class GameDefines
{
    public NNavyDefines NNavy { get; init; } = new();
    public NAirDefines NAir { get; init; } = new();
    public NMilitaryDefines NMilitary { get; init; } = new();
    public NDoctrinesDefines NDoctrines { get; init; } = new();

    /// <summary>S-NIGHT: the day/night terminator's map constants.</summary>
    public NGraphicsDefines NGraphics { get; init; } = new();

    /// <summary>
    /// Values the combat model needs that are not in any defines file (hard-coded in the executable, or not yet located).
    /// </summary>
    public HardcodedValues Hardcoded { get; init; } = new();

    /// <summary>Static modifier blocks naval combat applies directly (screening bonuses, experience, night, weather).</summary>
    public StaticModifiers StaticModifiers { get; init; } = new();

    /// <summary>S-WEATHER-SIM: the weather simulation settings of <c>common/weather.txt</c>.</summary>
    public WeatherSettings Weather { get; init; } = new();

    /// <summary>
    /// Human-readable list of the define sources applied on top of the built-in vanilla defaults, in order.
    /// </summary>
    public List<string> Sources { get; init; } = ["built-in vanilla 1.19.3"];

    public static GameDefines Vanilla()
    {
        return new GameDefines();
    }

    /// <summary>
    /// Checks the array shapes the combat rules index into. Mods may change array lengths (the game accepts
    /// any number of piercing tiers), but the related arrays must stay consistent.
    /// </summary>
    public List<string> Validate()
    {
        var problems = new List<string>();
        var navy = NNavy;

        if (navy.BASE_GUN_COOLDOWNS.Length < 3)
        {
            problems.Add($"NNavy.BASE_GUN_COOLDOWNS needs 3 values {{ heavy, torpedo, light }}, got {navy.BASE_GUN_COOLDOWNS.Length}.");
        }

        if (navy.GUN_HIT_PROFILES.Length < 3)
        {
            problems.Add($"NNavy.GUN_HIT_PROFILES needs 3 values {{ heavy, torpedo, light }}, got {navy.GUN_HIT_PROFILES.Length}.");
        }

        var tiers = navy.NAVY_PIERCING_THRESHOLDS.Length;

        if (tiers < 1 ||
            navy.NAVY_PIERCING_THRESHOLD_DAMAGE_VALUES.Length != tiers ||
            navy.NAVY_PIERCING_THRESHOLD_CRITICAL_VALUES.Length != tiers)
        {
            problems.Add(
                $"NNavy.NAVY_PIERCING_THRESHOLDS ({tiers}), NAVY_PIERCING_THRESHOLD_DAMAGE_VALUES ({navy.NAVY_PIERCING_THRESHOLD_DAMAGE_VALUES.Length}) " +
                $"and NAVY_PIERCING_THRESHOLD_CRITICAL_VALUES ({navy.NAVY_PIERCING_THRESHOLD_CRITICAL_VALUES.Length}) must have the same, non-zero length.");
        }

        return problems;
    }
}
