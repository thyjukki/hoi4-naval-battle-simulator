namespace NavySimulator.GameData.Defines;

/// <summary>
/// Defines grouped by category (e.g. <c>NNavy</c>, <c>NAir</c>), keyed by define name.
/// Later sources override earlier ones, matching how the game applies mod define files after vanilla.
/// </summary>
public sealed class DefinesTable
{
    private readonly Dictionary<string, Dictionary<string, DefineValue>> categories = new(StringComparer.Ordinal);

    public IEnumerable<string> Categories => categories.Keys;

    /// <summary>Statements or values that could not be evaluated and were skipped (e.g. references to other defines).</summary>
    public List<string> Warnings { get; } = [];

    public void Set(string category, string key, DefineValue value)
    {
        if (!categories.TryGetValue(category, out var entries))
        {
            entries = new Dictionary<string, DefineValue>(StringComparer.Ordinal);
            categories[category] = entries;
        }

        entries[key] = value;
    }

    public IReadOnlyDictionary<string, DefineValue> GetCategory(string category)
    {
        return categories.TryGetValue(category, out var entries)
            ? entries
            : new Dictionary<string, DefineValue>(StringComparer.Ordinal);
    }

    public DefineValue? Get(string category, string key)
    {
        return categories.TryGetValue(category, out var entries) && entries.TryGetValue(key, out var value) ? value : null;
    }

    public void Merge(DefinesTable overrides)
    {
        Warnings.AddRange(overrides.Warnings);

        foreach (var category in overrides.categories)
        {
            foreach (var entry in category.Value)
            {
                Set(category.Key, entry.Key, entry.Value);
            }
        }
    }
}
