using System.Collections;

namespace NavySimulator.GameData.Overrides;

/// <summary>A collection of imported items an override set can change: its data folder, item type and catalog list.</summary>
public sealed class OverrideCollection(string name, string label, Type itemType, Func<ContentCatalog, IList> items)
{
    /// <summary>The data folder, e.g. <c>leader-traits</c>; override sets name the collection by it.</summary>
    public string Name { get; } = name;

    public string Label { get; } = label;

    public Type ItemType { get; } = itemType;

    public IList Items(ContentCatalog catalog)
    {
        return items(catalog);
    }

    public object? Find(ContentCatalog catalog, string id)
    {
        return Items(catalog).Cast<object>().FirstOrDefault(item => string.Equals(IdOf(item), id, StringComparison.Ordinal));
    }

    public static string IdOf(object item)
    {
        return item.GetType().GetProperty("ID")?.GetValue(item) as string ?? string.Empty;
    }

    public static string? NameOf(object item)
    {
        return item.GetType().GetProperty("Name")?.GetValue(item) as string;
    }
}
