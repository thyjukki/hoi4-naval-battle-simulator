using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace NavySimulator.GameData.Import;

/// <summary>
/// Writes a <see cref="PlaneImport"/> to <c>planes/planes.json</c> of a data directory. The folder belongs to the import:
/// its old JSON files are deleted first. Empty lists and false flags are left out.
/// </summary>
public static class PlaneWriter
{
    public const string Folder = "planes";

    private static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true,
        NewLine = "\n",
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
    };

    /// <summary>Writes the file and returns it, data-directory relative.</summary>
    public static IReadOnlyList<string> Write(string dataDirectory, PlaneImport import)
    {
        var file = $"{Folder}/planes.json";
        var document = new JsonObject
        {
            ["$schema"] = "../schemas/planes.schema.json",
            ["planes"] = new JsonArray(import.Planes.Select(plane => CatalogWriter.Serialize(plane)).ToArray())
        };
        var directory = Path.Combine(dataDirectory, Folder);
        Directory.CreateDirectory(directory);

        foreach (var old in Directory.GetFiles(directory, "*.json"))
        {
            File.Delete(old);
        }

        DoctrineWriter.Prune(document);
        File.WriteAllText(Path.Combine(dataDirectory, file), document.ToJsonString(Options) + "\n");
        return [file];
    }
}
