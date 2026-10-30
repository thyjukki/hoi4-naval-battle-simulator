namespace NavySimulator.GameData.Contracts;

/// <summary>
/// S-NATIONAL-EFFECT: one effect statement of a history file or focus reward, by its game name: <c>add_ideas</c>,
/// <c>remove_ideas</c>, <c>add_dynamic_modifier</c>, <c>remove_dynamic_modifier</c>, <c>set_variable</c>,
/// <c>add_to_variable</c>, <c>subtract_from_variable</c> or <c>multiply_variable</c>.
/// </summary>
public class NationalEffectDto
{
    public string Effect { get; set; } = string.Empty;

    /// <summary>The idea, dynamic modifier or variable.</summary>
    public string Target { get; set; } = string.Empty;

    /// <summary>The variable operand.</summary>
    public double? Value { get; set; }

    /// <summary>
    /// The statement is inside an <c>if</c> (or a scripted effect's): the simulator assumes the first branch is taken
    /// and the <c>else</c> branches are not; this is the <c>limit</c> as script text.
    /// </summary>
    public string? Condition { get; set; }

    /// <summary>A timed idea or modifier: its days. The simulator treats it as active.</summary>
    public int? Days { get; set; }
}
