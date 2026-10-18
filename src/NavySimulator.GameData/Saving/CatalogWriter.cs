using System.Collections;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;
using NavySimulator.GameData.Contracts;

namespace NavySimulator.GameData;

/// <summary>
/// Writes edited content back to the data directory's JSON files. An item is replaced in place (matched by ID) or
/// appended; other entries of the file are kept as they are. Files keep their line endings.
/// </summary>
public static class CatalogWriter
{
    public const string ShipDesignsFolder = "ship-designs";
    public const string FleetsFolder = "force-compositions";

    // Lists that are always written, even when empty, because the files have always spelled them out.
    private static readonly HashSet<string> AlwaysWrittenCollections = new(["moduleIDs", "researchIDs", "spiritIDs"], StringComparer.Ordinal);

    private static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        TypeInfoResolver = new DefaultJsonTypeInfoResolver { Modifiers = { SkipEmptyOptionalCollections } }
    };

    public static void SaveShipDesign(string dataDirectory, string relativeFile, ShipDesignDto design, string? previousId = null)
    {
        Upsert(dataDirectory, relativeFile, "shipDesigns", "../schemas/ship-designs.schema.json", design.ID, previousId, Serialize(design));
    }

    public static bool DeleteShipDesign(string dataDirectory, string relativeFile, string id)
    {
        return Remove(dataDirectory, relativeFile, "shipDesigns", id);
    }

    public static void SaveFleet(string dataDirectory, string relativeFile, FleetDto fleet, string? previousId = null)
    {
        Upsert(dataDirectory, relativeFile, "fleets", "../schemas/force-compositions.schema.json", fleet.ID, previousId, Serialize(fleet));
    }

    public static bool DeleteFleet(string dataDirectory, string relativeFile, string id)
    {
        return Remove(dataDirectory, relativeFile, "fleets", id);
    }

    /// <summary>Writes a scenario file in the data directory root, replacing the <c>battleScenario</c> block.</summary>
    public static void SaveScenario(string dataDirectory, string fileName, BattleScenarioDto scenario)
    {
        var path = ResolvePath(dataDirectory, fileName);
        var (root, newLine) = ReadOrCreate(path, "schemas/battle-scenario.schema.json");
        root["battleScenario"] = Serialize(scenario);
        Write(path, root, newLine);
    }

    public static JsonNode Serialize<T>(T value)
    {
        return JsonSerializer.SerializeToNode(value, Options)!;
    }

    private static void Upsert(string dataDirectory, string relativeFile, string rootProperty, string schema, string id, string? previousId, JsonNode item)
    {
        var path = ResolvePath(dataDirectory, relativeFile);
        var (root, newLine) = ReadOrCreate(path, schema);
        var items = ItemsOf(root, rootProperty, path);
        var index = IndexOf(items, previousId ?? id);

        if (index < 0 && previousId is not null)
        {
            index = IndexOf(items, id);
        }

        if (index >= 0)
        {
            items[index] = item;
        }
        else
        {
            items.Add(item);
        }

        Write(path, root, newLine);
    }

    private static bool Remove(string dataDirectory, string relativeFile, string rootProperty, string id)
    {
        var path = ResolvePath(dataDirectory, relativeFile);

        if (!File.Exists(path))
        {
            return false;
        }

        var (root, newLine) = ReadOrCreate(path, string.Empty);
        var items = ItemsOf(root, rootProperty, path);
        var index = IndexOf(items, id);

        if (index < 0)
        {
            return false;
        }

        items.RemoveAt(index);
        Write(path, root, newLine);
        return true;
    }

    private static int IndexOf(JsonArray items, string id)
    {
        for (var i = 0; i < items.Count; i++)
        {
            if (items[i] is JsonObject item &&
                item.FirstOrDefault(property => property.Key.Equals("id", StringComparison.OrdinalIgnoreCase)).Value is JsonValue value &&
                value.TryGetValue<string>(out var itemId) &&
                itemId.Equals(id, StringComparison.Ordinal))
            {
                return i;
            }
        }

        return -1;
    }

    private static JsonArray ItemsOf(JsonObject root, string rootProperty, string path)
    {
        if (root[rootProperty] is JsonArray items)
        {
            return items;
        }

        if (root.ContainsKey(rootProperty))
        {
            throw new InvalidDataException($"'{rootProperty}' in {path} is not an array.");
        }

        items = [];
        root[rootProperty] = items;
        return items;
    }

    private static (JsonObject Root, string NewLine) ReadOrCreate(string path, string schema)
    {
        if (!File.Exists(path))
        {
            return (new JsonObject { ["$schema"] = schema }, "\n");
        }

        var text = File.ReadAllText(path);
        var newLine = text.Contains("\r\n", StringComparison.Ordinal) ? "\r\n" : "\n";
        var root = JsonNode.Parse(text) as JsonObject ?? throw new InvalidDataException($"{path} does not contain a JSON object.");
        return (root, newLine);
    }

    private static void Write(string path, JsonObject root, string newLine)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        var options = new JsonSerializerOptions(Options) { NewLine = newLine };
        File.WriteAllText(path, root.ToJsonString(options) + newLine);
    }

    private static string ResolvePath(string dataDirectory, string relativeFile)
    {
        var root = Path.GetFullPath(dataDirectory);
        var path = Path.GetFullPath(Path.Combine(root, relativeFile));

        if (!path.StartsWith(root.TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase) ||
            !path.EndsWith(".json", StringComparison.OrdinalIgnoreCase))
        {
            throw new ArgumentException($"'{relativeFile}' is not a .json file inside the data directory.", nameof(relativeFile));
        }

        return path;
    }

    private static void SkipEmptyOptionalCollections(JsonTypeInfo typeInfo)
    {
        foreach (var property in typeInfo.Properties)
        {
            if (typeof(IEnumerable).IsAssignableFrom(property.PropertyType) &&
                property.PropertyType != typeof(string) &&
                !AlwaysWrittenCollections.Contains(property.Name))
            {
                property.ShouldSerialize = (_, value) => value is not ICollection { Count: 0 };
            }
        }
    }
}
