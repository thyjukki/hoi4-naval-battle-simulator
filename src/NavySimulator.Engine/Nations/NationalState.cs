namespace NavySimulator.Engine.Nations;

/// <summary>S-NATIONAL: a side's resolved national state: active national spirits and dynamic modifiers, in the order added, and variables.</summary>
public sealed class NationalState(IReadOnlyList<string> spirits, IReadOnlyList<string> dynamicModifiers, IReadOnlyDictionary<string, double> variables)
{
    public static NationalState Empty { get; } = new([], [], new Dictionary<string, double>());

    public IReadOnlyList<string> Spirits { get; } = spirits;
    public IReadOnlyList<string> DynamicModifiers { get; } = dynamicModifiers;
    public IReadOnlyDictionary<string, double> Variables { get; } = variables;
}
