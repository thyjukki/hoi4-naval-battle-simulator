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

    /// <summary>
    /// Values the combat model needs that are not in any defines file (hard-coded in the executable, or not yet located).
    /// </summary>
    public HardcodedValues Hardcoded { get; init; } = new();

    /// <summary>
    /// Human-readable list of the define sources applied on top of the built-in vanilla defaults, in order.
    /// </summary>
    public List<string> Sources { get; init; } = ["built-in vanilla 1.17"];

    public static GameDefines Vanilla()
    {
        return new GameDefines();
    }
}
