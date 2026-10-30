using NavySimulator.Engine.Doctrines;

namespace NavySimulator.Engine.Nations;

/// <summary>
/// S-NATIONAL-SPIRIT: a national spirit (an idea of a country's own idea categories, not a spirit slot), with the
/// effects it gives its country while active (S-DOCTRINE-EFFECT).
/// </summary>
public sealed class NationalSpirit(string id, string category, CountryEffects effects)
{
    public string ID { get; } = id;

    /// <summary>The idea category, e.g. <c>country</c> or <c>hidden_ideas</c>.</summary>
    public string Category { get; } = category;

    public CountryEffects Effects { get; } = effects;
}
