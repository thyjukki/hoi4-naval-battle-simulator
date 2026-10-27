using System.Text.Json.Nodes;
using NavySimulator.Engine.Defines;
using NavySimulator.GameData;
using NavySimulator.GameData.Contracts;
using NavySimulator.GameData.Defines;
using NavySimulator.GameData.Overrides;

namespace NavySimulator.Web.Services;

/// <summary>
/// What the Overrides page shows of a set: its defines, static modifiers and content changes next to the values they
/// replace (the defines before the set, the data directory's content), and the defines, blocks and items to pick from.
/// </summary>
public static class OverrideInfo
{
    /// <summary>Every define the simulator reads (<c>NNavy.KEY</c>), sorted.</summary>
    public static IReadOnlyList<string> DefineKeys(GameDefines defines)
    {
        return DefinesBinder.Values(defines)
            .Where(value => OverrideValues.FromDefine(value.Value) is not null)
            .Select(value => value.Key)
            .Order(StringComparer.Ordinal)
            .ToList();
    }

    /// <summary>The define's value in <paramref name="defines"/>, or null when the simulator does not read it.</summary>
    public static JsonNode? DefineValue(GameDefines defines, string key)
    {
        var match = DefinesBinder.Values(defines).FirstOrDefault(value => value.Key == key);
        return match.Key is null ? null : OverrideValues.FromDefine(match.Value);
    }

    public static IReadOnlyList<OverrideDefineRow> Defines(OverrideSetDto set, GameDefines baseDefines)
    {
        var values = DefinesBinder.Values(baseDefines).ToDictionary(value => value.Key, value => value.Value, StringComparer.Ordinal);
        return set.Defines
            .OrderBy(define => define.Key, StringComparer.Ordinal)
            .Select(define => new OverrideDefineRow(define.Key,
                values.TryGetValue(define.Key, out var value) ? OverrideValues.FromDefine(value) : null,
                define.Value,
                values.ContainsKey(define.Key)))
            .ToList();
    }

    public static IReadOnlyList<OverrideModifierRow> StaticModifiers(OverrideSetDto set, GameDefines baseDefines)
    {
        return set.StaticModifiers
            .OrderBy(block => block.Key, StringComparer.Ordinal)
            .SelectMany(block => block.Value
                .OrderBy(modifier => modifier.Key, StringComparer.Ordinal)
                .Select(modifier => new OverrideModifierRow(block.Key, modifier.Key,
                    baseDefines.StaticModifiers.Blocks.TryGetValue(block.Key, out var values) && values.TryGetValue(modifier.Key, out var value) ? value : null,
                    modifier.Value,
                    Engine.Defines.StaticModifiers.UsedBlocks.Contains(block.Key))))
            .ToList();
    }

    /// <summary>The keys a static modifier block has before the set.</summary>
    public static IReadOnlyList<string> ModifierKeys(GameDefines baseDefines, string block)
    {
        return baseDefines.StaticModifiers.Blocks.TryGetValue(block, out var values) ? values.Keys.Order(StringComparer.Ordinal).ToList() : [];
    }

    /// <summary>The set's content changes with the values they replace in <paramref name="catalog"/> (the data directory's).</summary>
    public static IReadOnlyList<OverrideContentRow> Content(OverrideSetDto set, ContentCatalog catalog)
    {
        return set.Content.Select(change =>
        {
            var collection = OverrideCollections.Find(change.Collection);
            var item = collection?.Find(catalog, change.ID);
            JsonNode? value = null;

            if (item is not null)
            {
                try
                {
                    value = ContentPath.Read(OverrideApplier.ToJson(item), change.Path);
                }
                catch (FormatException)
                {
                    // A malformed path; the set's warnings say why.
                }
            }

            return new OverrideContentRow(change, collection?.Label ?? change.Collection, item is null ? null : OverrideCollection.NameOf(item), item is not null, value);
        }).ToList();
    }

    /// <summary>The items of a collection, sorted by name, for the item picker.</summary>
    public static IReadOnlyList<(string Id, string? Name)> Items(ContentCatalog catalog, string collection)
    {
        return OverrideCollections.Find(collection) is { } found
            ? found.Items(catalog).Cast<object>()
                .Select(item => (OverrideCollection.IdOf(item), OverrideCollection.NameOf(item)))
                .OrderBy(item => item.Item2 ?? item.Item1, StringComparer.CurrentCultureIgnoreCase)
                .ToList()
            : [];
    }

    /// <summary>The changeable fields of an item in <paramref name="catalog"/>, or none when it does not exist.</summary>
    public static IReadOnlyList<ContentField> Fields(ContentCatalog catalog, string collection, string id)
    {
        return OverrideCollections.Find(collection)?.Find(catalog, id) is { } item ? ContentFields.Describe(item) : [];
    }

    /// <summary>
    /// Whether <paramref name="path"/> lies in the part of an item <paramref name="prefix"/> names (a list entry such as
    /// <c>subdoctrines[x]</c>, or a field); an empty prefix is the whole item.
    /// </summary>
    public static bool IsUnder(string path, string? prefix)
    {
        return string.IsNullOrEmpty(prefix) || path == prefix || path.StartsWith(prefix + ".", StringComparison.Ordinal);
    }

    /// <summary>The set's changes to an item, or to the part of it under <paramref name="prefix"/>.</summary>
    public static IReadOnlyList<ContentOverrideDto> ChangesOf(OverrideSetDto? set, string collection, string id, string? prefix = null)
    {
        return set is null
            ? []
            : set.Content.Where(change => string.Equals(change.Collection, collection, StringComparison.OrdinalIgnoreCase) &&
                                          change.ID == id && IsUnder(change.Path, prefix)).ToList();
    }

    /// <summary>One line per change: "field: before → after", the field relative to <paramref name="prefix"/>.</summary>
    public static string Describe(OverrideSetDto? set, ContentCatalog catalog, string collection, string id, string? prefix = null)
    {
        var item = OverrideCollections.Find(collection)?.Find(catalog, id);
        var json = item is null ? null : OverrideApplier.ToJson(item);
        return string.Join("\n", ChangesOf(set, collection, id, prefix).Select(change =>
        {
            JsonNode? before = null;

            try
            {
                before = json is null ? null : ContentPath.Read(json, change.Path);
            }
            catch (FormatException)
            {
                // A malformed path; the set's warnings say why.
            }

            return $"{RelativePath(change.Path, prefix)}: {OverrideValues.Format(before)} → {(change.Value is null ? "removed" : OverrideValues.Format(change.Value))}";
        }));
    }

    /// <summary>The fields of an item (or of its part under <paramref name="prefix"/>) in <paramref name="catalog"/>.</summary>
    public static IReadOnlyList<ContentField> Fields(ContentCatalog catalog, string collection, string id, string? prefix)
    {
        return Fields(catalog, collection, id).Where(field => IsUnder(field.Path, prefix)).ToList();
    }

    /// <summary>A path as shown inside the part <paramref name="prefix"/> names ("(item)" for the part itself).</summary>
    public static string RelativePath(string path, string? prefix)
    {
        var relative = string.IsNullOrEmpty(prefix) ? path : path == prefix ? string.Empty : path[(prefix.Length + 1)..];
        return relative.Length == 0 ? "(item)" : relative;
    }

    /// <summary>"item name (id)" or the ID alone.</summary>
    public static string ItemLabel(string id, string? name)
    {
        return string.IsNullOrWhiteSpace(name) || name == id ? id : $"{name} ({id})";
    }
}
