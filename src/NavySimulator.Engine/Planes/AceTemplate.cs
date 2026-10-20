namespace NavySimulator.Engine;

/// <summary>
/// A-ACE [PDX]: an air ace template of <c>common/aces</c> (<c>modifiers = { id = { type chance effect } }</c>): the plane
/// sub-unit types it can come to, its weight among them and its modifiers (e.g. <c>naval_strike_targetting_factor</c>).
/// </summary>
public sealed record AceTemplate(string ID, IReadOnlySet<string> Types, double Chance, IReadOnlyDictionary<string, double> Effects)
{
    public double Effect(string key)
    {
        return Effects.TryGetValue(key, out var value) ? value : 0;
    }
}
