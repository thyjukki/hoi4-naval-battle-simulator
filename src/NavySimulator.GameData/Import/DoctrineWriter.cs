using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace NavySimulator.GameData.Import;

/// <summary>
/// Writes a <see cref="DoctrineImport"/> to a data directory: <c>doctrines/&lt;folder&gt;.json</c> (one file per doctrine
/// folder) and <c>spirits/navy-spirits.json</c> and <c>spirits/air-spirits.json</c>. These folders belong to the import:
/// their old JSON files are deleted first. Empty lists, empty blocks and false flags are left out.
/// </summary>
public static class DoctrineWriter
{
    public const string DoctrinesFolder = "doctrines";
    public const string SpiritsFolder = "spirits";

    private static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true,
        NewLine = "\n",
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
    };

    /// <summary>Writes the files and returns them, data-directory relative.</summary>
    public static IReadOnlyList<string> Write(string dataDirectory, DoctrineImport import)
    {
        var files = new Dictionary<string, JsonObject>(StringComparer.Ordinal);

        foreach (var folder in import.Folders)
        {
            var node = (JsonObject)CatalogWriter.Serialize(folder);
            files[$"{DoctrinesFolder}/{folder.ID}.json"] = WithSchema("../schemas/doctrines.schema.json", node);
        }

        foreach (var group in import.Spirits.GroupBy(spirit => spirit.Category.StartsWith("air", StringComparison.Ordinal) ? "air-spirits" : "navy-spirits"))
        {
            var spirits = new JsonArray(group.Select(spirit => CatalogWriter.Serialize(spirit)).ToArray());
            files[$"{SpiritsFolder}/{group.Key}.json"] = new JsonObject { ["$schema"] = "../schemas/spirits.schema.json", ["spirits"] = spirits };
        }

        foreach (var folder in new[] { DoctrinesFolder, SpiritsFolder })
        {
            var directory = Path.Combine(dataDirectory, folder);
            Directory.CreateDirectory(directory);

            foreach (var old in Directory.GetFiles(directory, "*.json"))
            {
                File.Delete(old);
            }
        }

        foreach (var (file, document) in files)
        {
            Prune(document);
            File.WriteAllText(Path.Combine(dataDirectory, file), document.ToJsonString(Options) + "\n");
        }

        return files.Keys.Order(StringComparer.Ordinal).ToList();
    }

    private static JsonObject WithSchema(string schema, JsonObject node)
    {
        var document = new JsonObject { ["$schema"] = schema };

        foreach (var (key, value) in node.ToList())
        {
            node.Remove(key);
            document[key] = value;
        }

        return document;
    }

    /// <summary>Removes false flags and objects left empty, depth first.</summary>
    internal static void Prune(JsonNode? node)
    {
        switch (node)
        {
            case JsonObject item:
                foreach (var (key, value) in item.ToList())
                {
                    Prune(value);

                    if (value is JsonObject { Count: 0 } || (value is JsonValue flag && flag.TryGetValue<bool>(out var boolean) && !boolean))
                    {
                        item.Remove(key);
                    }
                }

                break;
            case JsonArray items:
                foreach (var value in items)
                {
                    Prune(value);
                }

                break;
        }
    }
}
