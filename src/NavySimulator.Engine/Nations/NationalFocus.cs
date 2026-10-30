namespace NavySimulator.Engine.Nations;

/// <summary>
/// S-NATIONAL-FOCUS: a national focus of a country's focus tree, with the effects of its completion reward the simulator
/// follows (S-NATIONAL-EFFECT) and the focuses it excludes.
/// </summary>
public sealed class NationalFocus(string id, IReadOnlyList<NationalEffect> effects, IReadOnlyList<string> mutuallyExclusive)
{
    public string ID { get; } = id;
    public IReadOnlyList<NationalEffect> Effects { get; } = effects;

    /// <summary>The focuses its <c>mutually_exclusive</c> block names.</summary>
    public IReadOnlyList<string> MutuallyExclusive { get; } = mutuallyExclusive;
}
