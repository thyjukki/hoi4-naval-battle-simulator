using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace NavySimulator.GameData.Import;

/// <summary>
/// Writes a <see cref="RegionImport"/> to a data directory: <c>sea-regions/sea-regions.json</c> and
/// <c>naval-terrains/naval-terrains.json</c>. These folders belong to the import: their old JSON files are deleted first.
/// Empty lists and blocks are left out.
/// </summary>
public static class RegionWriter
{
    public const string RegionsFolder = "sea-regions";
    public const string TerrainsFolder = "naval-terrains";

    private static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true,
        NewLine = "\n",
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
    };

    /// <summary>Writes the files and returns them, data-directory relative.</summary>
    public static IReadOnlyList<string> Write(string dataDirectory, RegionImport import)
    {
        var files = new Dictionary<string, JsonObject>(StringComparer.Ordinal)
        {
            [$"{RegionsFolder}/sea-regions.json"] = new()
            {
                ["$schema"] = "../schemas/sea-regions.schema.json",
                ["regions"] = new JsonArray(import.Regions.Select(region => CatalogWriter.Serialize(region)).ToArray())
            },
            [$"{TerrainsFolder}/naval-terrains.json"] = new()
            {
                ["$schema"] = "../schemas/naval-terrains.schema.json",
                ["terrains"] = new JsonArray(import.Terrains.Select(terrain => CatalogWriter.Serialize(terrain)).ToArray())
            }
        };

        foreach (var folder in new[] { RegionsFolder, TerrainsFolder })
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
            DoctrineWriter.Prune(document);
            File.WriteAllText(Path.Combine(dataDirectory, file), document.ToJsonString(Options) + "\n");
        }

        return files.Keys.Order(StringComparer.Ordinal).ToList();
    }
}
