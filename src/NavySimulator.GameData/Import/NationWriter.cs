using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace NavySimulator.GameData.Import;

/// <summary>
/// Writes a <see cref="NationImport"/> to a data directory: <c>nations/&lt;TAG&gt;.json</c> per country. Only the imported
/// countries' files are replaced; other countries' files stay. Empty lists and blocks are left out.
/// </summary>
public static class NationWriter
{
    public const string Folder = "nations";

    private static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true,
        NewLine = "\n",
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
    };

    /// <summary>Writes the files and returns them, data-directory relative.</summary>
    public static IReadOnlyList<string> Write(string dataDirectory, NationImport import)
    {
        Directory.CreateDirectory(Path.Combine(dataDirectory, Folder));
        var files = new List<string>();

        foreach (var nation in import.Nations)
        {
            var file = $"{Folder}/{nation.ID}.json";
            var document = new JsonObject
            {
                ["$schema"] = "../schemas/nations.schema.json",
                ["nations"] = new JsonArray(CatalogWriter.Serialize(nation))
            };
            DoctrineWriter.Prune(document);
            File.WriteAllText(Path.Combine(dataDirectory, file), document.ToJsonString(Options) + "\n");
            files.Add(file);
        }

        return files;
    }
}
