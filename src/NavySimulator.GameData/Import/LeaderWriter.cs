using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace NavySimulator.GameData.Import;

/// <summary>
/// Writes a <see cref="LeaderImport"/> to a data directory: <c>leader-skills/navy-skills.json</c>,
/// <c>leader-traits/navy-traits.json</c> and <c>admirals/admirals.json</c>. These folders belong to the import: their old
/// JSON files are deleted first. Empty lists, empty blocks and false flags are left out.
/// </summary>
public static class LeaderWriter
{
    public const string SkillsFolder = "leader-skills";
    public const string TraitsFolder = "leader-traits";
    public const string AdmiralsFolder = "admirals";

    private static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true,
        NewLine = "\n",
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
    };

    /// <summary>Writes the files and returns them, data-directory relative.</summary>
    public static IReadOnlyList<string> Write(string dataDirectory, LeaderImport import)
    {
        var files = new Dictionary<string, JsonObject>(StringComparer.Ordinal)
        {
            [$"{SkillsFolder}/navy-skills.json"] = new()
            {
                ["$schema"] = "../schemas/leader-skills.schema.json",
                ["skills"] = new JsonArray(import.Skills.Select(skill => CatalogWriter.Serialize(skill)).ToArray())
            },
            [$"{TraitsFolder}/navy-traits.json"] = new()
            {
                ["$schema"] = "../schemas/leader-traits.schema.json",
                ["traits"] = new JsonArray(import.Traits.Select(trait => CatalogWriter.Serialize(trait)).ToArray())
            },
            [$"{AdmiralsFolder}/admirals.json"] = new()
            {
                ["$schema"] = "../schemas/admirals.schema.json",
                ["admirals"] = new JsonArray(import.Admirals.Select(admiral => CatalogWriter.Serialize(admiral)).ToArray())
            }
        };

        foreach (var folder in new[] { SkillsFolder, TraitsFolder, AdmiralsFolder })
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
