namespace NavySimulator.Engine.Nations;

/// <summary>
/// S-NATIONAL: what a country's history and focus tree give its navy: its national spirits and dynamic modifiers, the
/// history file's start (S-NATIONAL-HISTORY) and the focuses with such effects, in tree order.
/// </summary>
public sealed class Nation(
    string tag,
    IReadOnlyDictionary<string, NationalSpirit> spirits,
    IReadOnlyDictionary<string, DynamicModifier> dynamicModifiers,
    IReadOnlyList<NationalEffect> history,
    IReadOnlyList<NationalFocus> focuses)
{
    /// <summary>The country tag, e.g. <c>JAP</c>.</summary>
    public string Tag { get; } = tag;

    public IReadOnlyDictionary<string, NationalSpirit> Spirits { get; } = spirits;
    public IReadOnlyDictionary<string, DynamicModifier> DynamicModifiers { get; } = dynamicModifiers;

    /// <summary>S-NATIONAL-HISTORY: the history file's effects at the 1936 start.</summary>
    public IReadOnlyList<NationalEffect> History { get; } = history;

    public IReadOnlyList<NationalFocus> Focuses { get; } = focuses;

    public NationalFocus? Focus(string id)
    {
        return Focuses.FirstOrDefault(focus => focus.ID == id);
    }
}
