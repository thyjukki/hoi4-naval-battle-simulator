using NavySimulator.Engine.Doctrines;

namespace NavySimulator.Engine;

/// <summary>
/// S-SPIRIT: a navy or air spirit (an idea in a spirit slot). Its effects work like a doctrine's (S-DOCTRINE-EFFECT): in
/// practice country modifiers.
/// </summary>
public class Spirit(string id, string category, IReadOnlyList<IReadOnlyList<string>> requiredDoctrines, CountryEffects effects)
{
    public string ID { get; } = id;

    /// <summary>The idea category (spirit slot), e.g. <c>naval_command_spirit</c>.</summary>
    public string Category { get; } = category;

    /// <summary>The <c>has_doctrine</c> conditions of its <c>available</c> trigger: each group needs one of its doctrines.</summary>
    public IReadOnlyList<IReadOnlyList<string>> RequiredDoctrines { get; } = requiredDoctrines;

    public CountryEffects Effects { get; } = effects;

    /// <summary>S-SPIRIT [PDX]: whether the side's doctrines meet every <c>has_doctrine</c> group.</summary>
    public bool IsAvailable(IEnumerable<DoctrineSetup> doctrines)
    {
        var setups = doctrines.ToList();
        return RequiredDoctrines.All(group => group.Any(id => DoctrineRules.HasDoctrine(setups, id)));
    }
}
