namespace NavySimulator.Engine.Stats;

/// <summary>
/// Game modifiers by their script names (e.g. <c>naval_retreat_speed</c>), as technologies, ideas and doctrines declare
/// them. Values of the same name add up; a name that is not in the set is 0.
/// </summary>
public sealed class ModifierSet
{
    private readonly Dictionary<string, double> values = new(StringComparer.Ordinal);

    public ModifierSet(IEnumerable<KeyValuePair<string, double>> values)
    {
        foreach (var (key, value) in values)
        {
            this.values[key] = this.values.GetValueOrDefault(key) + value;
        }
    }

    public static ModifierSet Empty { get; } = new([]);

    public IReadOnlyDictionary<string, double> Values => values;

    public double Get(string key)
    {
        return values.GetValueOrDefault(key);
    }

    public ModifierSet Add(ModifierSet other)
    {
        return new ModifierSet(values.Concat(other.values));
    }
}
