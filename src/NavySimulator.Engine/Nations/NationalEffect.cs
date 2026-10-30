namespace NavySimulator.Engine.Nations;

/// <summary>
/// S-NATIONAL-EFFECT: one effect statement of a country's history or of a focus reward: an idea or dynamic modifier
/// added or removed, or a variable changed. <see cref="Value"/> is the variable operand.
/// </summary>
public sealed class NationalEffect(NationalEffectKind kind, string target, double value = 0)
{
    public NationalEffectKind Kind { get; } = kind;

    /// <summary>The idea, dynamic modifier or variable the effect acts on.</summary>
    public string Target { get; } = target;

    public double Value { get; } = value;
}
