using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;
using NavySimulator.GameData.Contracts;

namespace NavySimulator.GameData.Overrides;

/// <summary>
/// Reads and writes override sets: one set per file in the data directory's <c>overrides/</c> folder, as
/// <c>{ "$schema": ..., "overrideSet": { ... } }</c>.
/// </summary>
public static class OverrideSetStore
{
    public const string Folder = "overrides";
    public const string Schema = "../schemas/override-set.schema.json";

    private static readonly JsonSerializerOptions ReadOptions = new() { PropertyNameCaseInsensitive = true };

    private static readonly JsonSerializerOptions WriteOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
    };

    /// <summary>The set files, data-directory relative with forward slashes (<c>overrides/x.json</c>).</summary>
    public static IReadOnlyList<string> ListFiles(string dataDirectory)
    {
        var directory = Path.Combine(dataDirectory, Folder);

        return Directory.Exists(directory)
            ? Directory.GetFiles(directory, "*.json").Select(path => $"{Folder}/{Path.GetFileName(path)}").OrderBy(path => path, StringComparer.OrdinalIgnoreCase).ToList()
            : [];
    }

    public static OverrideSetDto Read(string path)
    {
        var file = JsonSerializer.Deserialize<OverrideSetFileDto>(File.ReadAllText(path), ReadOptions)
                   ?? throw new InvalidDataException($"{path} is empty.");
        var set = file.OverrideSet;
        set.Defines ??= [];
        set.StaticModifiers ??= [];
        set.Content ??= [];

        if (string.IsNullOrWhiteSpace(set.ID))
        {
            set.ID = Path.GetFileNameWithoutExtension(path);
        }

        return set;
    }

    /// <summary>
    /// Reads a set given on a command line: a path to a set file, or the name of one in <c>overrides/</c> (with or
    /// without <c>.json</c>).
    /// </summary>
    public static OverrideSetDto Resolve(string reference, string dataDirectory)
    {
        var candidates = new[]
        {
            reference,
            Path.Combine(dataDirectory, reference),
            Path.Combine(dataDirectory, Folder, reference),
            Path.Combine(dataDirectory, Folder, reference + ".json")
        };

        var path = candidates.FirstOrDefault(File.Exists)
                   ?? throw new FileNotFoundException($"Override set '{reference}' was not found (a set file, or a name in {Folder}/).");
        return Read(path);
    }

    public static string Serialize(OverrideSetDto set)
    {
        return JsonSerializer.Serialize(new OverrideSetFileDto { Schema = Schema, OverrideSet = set }, WriteOptions);
    }

    public static void Write(string dataDirectory, string relativeFile, OverrideSetDto set)
    {
        var path = ResolvePath(dataDirectory, relativeFile);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, Serialize(set) + "\n");
    }

    public static void Delete(string dataDirectory, string relativeFile)
    {
        var path = ResolvePath(dataDirectory, relativeFile);

        if (File.Exists(path))
        {
            File.Delete(path);
        }
    }

    /// <summary>A file name for a new set: its ID with characters a file name cannot have replaced.</summary>
    public static string FileFor(string id)
    {
        var invalid = Path.GetInvalidFileNameChars();
        var name = new string(id.Trim().Select(character => invalid.Contains(character) ? '_' : character).ToArray());
        return $"{Folder}/{(name.Length == 0 ? "override-set" : name)}.json";
    }

    private static string ResolvePath(string dataDirectory, string relativeFile)
    {
        var root = Path.GetFullPath(Path.Combine(dataDirectory, Folder));
        var path = Path.GetFullPath(Path.Combine(dataDirectory, relativeFile));

        if (!path.StartsWith(root + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase) ||
            !path.EndsWith(".json", StringComparison.OrdinalIgnoreCase))
        {
            throw new ArgumentException($"'{relativeFile}' is not a .json file in {Folder}/.", nameof(relativeFile));
        }

        return path;
    }
}
