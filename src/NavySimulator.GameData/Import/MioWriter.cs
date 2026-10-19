using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace NavySimulator.GameData.Import;

/// <summary>
/// Writes a <see cref="MioImport"/> to a data directory: <c>mios/&lt;game file&gt;.json</c> (the MIOs of each organizations
/// file, e.g. <c>mios/USA_organization.json</c>), <c>mio-policies/naval-policies.json</c> and
/// <c>equipment-groups/naval-groups.json</c> and <c>countries/countries.json</c>. These folders belong to the import: their
/// old JSON files are deleted first.
/// Empty lists, empty blocks and false flags are left out.
/// </summary>
public static class MioWriter
{
    public const string MiosFolder = "mios";
    public const string PoliciesFolder = "mio-policies";
    public const string EquipmentGroupsFolder = "equipment-groups";
    public const string CountriesFolder = "countries";

    private static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true,
        NewLine = "\n",
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
    };

    /// <summary>Writes the files and returns them, data-directory relative.</summary>
    public static IReadOnlyList<string> Write(string dataDirectory, MioImport import)
    {
        var files = new Dictionary<string, JsonObject>(StringComparer.Ordinal);

        foreach (var group in import.Mios.GroupBy(item => FileName(item.File)))
        {
            var mios = new JsonArray(group.Select(item => CatalogWriter.Serialize(item.Mio)).ToArray());
            files[$"{MiosFolder}/{group.Key}.json"] = new JsonObject { ["$schema"] = "../schemas/mios.schema.json", ["mios"] = mios };
        }

        files[$"{PoliciesFolder}/naval-policies.json"] = new JsonObject
        {
            ["$schema"] = "../schemas/mio-policies.schema.json",
            ["policies"] = new JsonArray(import.Policies.Select(policy => CatalogWriter.Serialize(policy)).ToArray())
        };
        files[$"{EquipmentGroupsFolder}/naval-groups.json"] = new JsonObject
        {
            ["$schema"] = "../schemas/equipment-groups.schema.json",
            ["equipmentGroups"] = new JsonArray(import.EquipmentGroups.Select(group => CatalogWriter.Serialize(group)).ToArray())
        };

        files[$"{CountriesFolder}/countries.json"] = new JsonObject
        {
            ["$schema"] = "../schemas/countries.schema.json",
            ["countries"] = new JsonArray(import.Countries.Select(country => CatalogWriter.Serialize(country)).ToArray())
        };

        foreach (var folder in new[] { MiosFolder, PoliciesFolder, EquipmentGroupsFolder, CountriesFolder })
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

    // "common/.../USA_organization.txt" -> "USA_organization"; the game has a file name with a trailing space.
    private static string FileName(string gameFile)
    {
        return Path.GetFileNameWithoutExtension(gameFile).Trim().Replace(' ', '_');
    }
}
