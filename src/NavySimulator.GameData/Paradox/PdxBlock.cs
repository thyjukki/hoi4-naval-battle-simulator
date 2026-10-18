namespace NavySimulator.GameData.Paradox;

/// <summary>
/// A <c>{ ... }</c> block. Keys may repeat (e.g. several <c>module = { }</c> entries), so entries are kept in order.
/// </summary>
public sealed class PdxBlock
{
    public PdxBlock(List<PdxEntry> entries)
    {
        Entries = entries;
    }

    public List<PdxEntry> Entries { get; }

    public IEnumerable<PdxEntry> KeyedEntries => Entries.Where(entry => entry.Key is not null);

    public IEnumerable<PdxValue> BareValues => Entries.Where(entry => entry.Key is null).Select(entry => entry.Value);

    public PdxValue? Get(string key)
    {
        return Entries.LastOrDefault(entry => string.Equals(entry.Key, key, StringComparison.Ordinal))?.Value;
    }

    public IEnumerable<PdxValue> GetAll(string key)
    {
        return Entries.Where(entry => string.Equals(entry.Key, key, StringComparison.Ordinal)).Select(entry => entry.Value);
    }

    public PdxBlock? GetBlock(string key)
    {
        return Get(key)?.Block;
    }

    public string? GetString(string key)
    {
        return Get(key)?.Scalar;
    }

    public double? GetDouble(string key)
    {
        var value = Get(key);
        return value is not null && value.TryGetDouble(out var result) ? result : null;
    }

    public bool? GetBool(string key)
    {
        var value = Get(key);
        return value is not null && value.TryGetBool(out var result) ? result : null;
    }

    /// <summary>
    /// Reads every keyed numeric scalar in the block, e.g. the contents of <c>add_stats = { ... }</c>.
    /// </summary>
    public Dictionary<string, double> GetNumericEntries()
    {
        var result = new Dictionary<string, double>(StringComparer.Ordinal);

        foreach (var entry in KeyedEntries)
        {
            if (entry.Value.TryGetDouble(out var number))
            {
                result[entry.Key!] = number;
            }
        }

        return result;
    }
}
