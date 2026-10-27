using System.Text.Json;
using System.Text.Json.Nodes;
using NavySimulator.Engine.Defines;
using NavySimulator.GameData.Contracts;
using NavySimulator.GameData.Defines;

namespace NavySimulator.GameData.Overrides;

/// <summary>
/// Applies an <see cref="OverrideSetDto"/>: its content changes to a copy of a catalog (the original stays as read
/// from disk), its defines and static modifiers to the <see cref="GameDefines"/> the define sources gave. A change that
/// cannot be applied (unknown item, path or define, wrong value type) is skipped with a warning.
/// </summary>
public static class OverrideApplier
{
    // The JSON shape of the data files: camelCase properties, dictionary keys as written.
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true
    };

    public static JsonObject ToJson(object item)
    {
        return (JsonObject)JsonSerializer.SerializeToNode(item, item.GetType(), JsonOptions)!;
    }

    /// <summary>A catalog with the set's content changes; the same catalog when the set changes no content.</summary>
    public static ContentCatalog ApplyContent(ContentCatalog catalog, OverrideSetDto set, List<string> warnings)
    {
        if (set.Content.Count == 0)
        {
            return catalog;
        }

        var copy = catalog.Copy();

        foreach (var group in set.Content.GroupBy(change => (Collection: change.Collection.ToLowerInvariant(), change.ID)))
        {
            var first = group.First();

            if (OverrideCollections.Find(first.Collection) is not { } collection)
            {
                warnings.Add(Warning(set, $"unknown collection '{first.Collection}' (one of {string.Join(", ", OverrideCollections.All.Select(c => c.Name))})."));
                continue;
            }

            var items = collection.Items(copy);
            var original = collection.Find(copy, first.ID);

            if (original is null)
            {
                warnings.Add(Warning(set, $"{collection.Name} has no '{first.ID}'."));
                continue;
            }

            var json = ToJson(original);
            var patched = original;

            foreach (var change in group)
            {
                if (TryApply(json, change, collection.ItemType, out var item, out var problem))
                {
                    json = ToJson(item!);
                    patched = item!;
                }
                else
                {
                    warnings.Add(Warning(set, $"{collection.Name}/{change.ID} {change.Path}: {problem}"));
                }
            }

            if (!ReferenceEquals(patched, original))
            {
                items[items.IndexOf(original)] = patched;

                if (catalog.SourceFileOf(original) is { } file)
                {
                    copy.SetSourceFile(patched, file);
                }
            }
        }

        return copy;
    }

    /// <summary>
    /// Applies the set's defines and static modifier keys. The applied values are added to <paramref name="report"/>,
    /// and the set to the defines' sources.
    /// </summary>
    public static void ApplyDefines(GameDefines defines, OverrideSetDto set, DefinesBindingReport? report, List<string> warnings)
    {
        var targets = DefinesBinder.Targets(defines);
        var table = new DefinesTable();

        foreach (var (key, node) in set.Defines)
        {
            var dot = key.IndexOf('.');
            var category = dot > 0 ? key[..dot] : string.Empty;
            var name = dot > 0 ? key[(dot + 1)..] : key;

            if (!targets.TryGetValue(category, out var target) || target.GetType().GetProperty(name) is null)
            {
                warnings.Add(Warning(set, $"define '{key}' is not read by the simulator (write Category.KEY, e.g. NNavy.COMBAT_BASE_HIT_CHANCE)."));
                continue;
            }

            if (OverrideValues.ToDefineValue(node) is not { } value)
            {
                warnings.Add(Warning(set, $"define '{key}': {OverrideValues.Format(node)} is not a number, boolean, string or list of numbers."));
                continue;
            }

            table.Set(category, name, value);
        }

        var applied = DefinesBinder.Apply(table, defines);
        warnings.AddRange(applied.TypeMismatches.Select(mismatch => Warning(set, $"define {mismatch}")));
        report?.Applied.AddRange(applied.Applied.Select(line => $"{line} (override set)"));

        foreach (var (block, values) in set.StaticModifiers)
        {
            if (!StaticModifiers.UsedBlocks.Contains(block))
            {
                warnings.Add(Warning(set, $"static modifier block '{block}' is not read by naval combat (read: {string.Join(", ", StaticModifiers.UsedBlocks)})."));
            }

            if (!defines.StaticModifiers.Blocks.TryGetValue(block, out var modifiers))
            {
                modifiers = new Dictionary<string, double>(StringComparer.Ordinal);
                defines.StaticModifiers.Blocks[block] = modifiers;
            }

            foreach (var (modifier, value) in values)
            {
                if (value is { } number)
                {
                    modifiers[modifier] = number;
                    report?.Applied.Add($"{block}.{modifier} = {OverrideValues.Format(number)} (override set)");
                }
                else
                {
                    modifiers.Remove(modifier);
                    report?.Applied.Add($"{block}.{modifier} removed (override set)");
                }
            }
        }

        defines.Sources.Add($"override set '{DisplayName(set)}'");
    }

    public static string DisplayName(OverrideSetDto set)
    {
        return string.IsNullOrWhiteSpace(set.Name) ? set.ID : set.Name;
    }

    private static bool TryApply(JsonObject json, ContentOverrideDto change, Type itemType, out object? item, out string problem)
    {
        item = null;

        try
        {
            if (ContentPath.Parse(change.Path) is [{ Id: null } only] && string.Equals(only.Name, "id", StringComparison.OrdinalIgnoreCase))
            {
                problem = "an item's ID cannot be changed.";
                return false;
            }

            var candidate = (JsonObject)json.DeepClone();
            ContentPath.Write(candidate, change.Path, change.Value);
            item = candidate.Deserialize(itemType, JsonOptions);

            if (item is null)
            {
                problem = "the item became empty.";
                return false;
            }

            // A property the item type does not have is dropped when reading, so check that the value arrived.
            if (change.Value is not null && !OverrideValues.Equal(ContentPath.Read(ToJson(item), change.Path), change.Value))
            {
                problem = $"not a field of {itemType.Name.Replace("Dto", string.Empty)}, or the value does not fit it.";
                return false;
            }

            problem = string.Empty;
            return true;
        }
        catch (Exception exception) when (exception is FormatException or InvalidOperationException or JsonException or NotSupportedException)
        {
            problem = exception.Message;
            return false;
        }
    }

    private static string Warning(OverrideSetDto set, string message)
    {
        return $"override set '{DisplayName(set)}': {message}";
    }
}
