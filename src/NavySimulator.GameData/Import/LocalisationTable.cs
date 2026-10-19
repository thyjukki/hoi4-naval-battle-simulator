using System.Text.RegularExpressions;

namespace NavySimulator.GameData.Import;

/// <summary>
/// The game's localisation for one language (<c>localisation/**/*_l_&lt;language&gt;.yml</c>) [PDX]. Files in a
/// <c>replace</c> folder win over the others; otherwise later files (mods) win. <see cref="Get"/> resolves
/// <c>$key$</c> references and drops colour codes.
/// </summary>
public sealed partial class LocalisationTable
{
    private readonly Dictionary<string, string> entries = new(StringComparer.Ordinal);

    public int Count => entries.Count;

    public static LocalisationTable Load(GameFileSystem files, string language = "english")
    {
        var table = new LocalisationTable();
        var paths = files.GetFiles("localisation", $"*_l_{language}.yml", recursive: true);
        var ordered = paths.Where(path => !IsReplace(files.RelativePath(path)))
            .Concat(paths.Where(path => IsReplace(files.RelativePath(path))));

        foreach (var path in ordered)
        {
            table.AddFile(File.ReadAllLines(path));
        }

        return table;
    }

    public void AddFile(IEnumerable<string> lines)
    {
        foreach (var line in lines)
        {
            var match = EntryLine().Match(line);

            if (match.Success)
            {
                entries[match.Groups["key"].Value] = match.Groups["text"].Value;
            }
        }
    }

    /// <summary>The text of a key, or null when the key is missing or empty.</summary>
    public string? Get(string key)
    {
        return Resolve(key, 0);
    }

    private string? Resolve(string key, int depth)
    {
        if (depth > 5 || !entries.TryGetValue(key, out var text) || text.Length == 0)
        {
            return null;
        }

        text = Reference().Replace(text, match => Resolve(match.Groups[1].Value, depth + 1) ?? match.Value);
        text = ColourCode().Replace(text, string.Empty).Replace("\\n", " ").Replace("\\\"", "\"").Trim();
        return text.Length == 0 ? null : text;
    }

    private static bool IsReplace(string relativePath)
    {
        return relativePath.Split('/').Contains("replace", StringComparer.OrdinalIgnoreCase);
    }

    [GeneratedRegex("""^\s*(?<key>[A-Za-z0-9_.\-]+):\d*\s*"(?<text>.*)"\s*(#.*)?$""")]
    private static partial Regex EntryLine();

    [GeneratedRegex(@"\$([A-Za-z0-9_.\-]+)\$")]
    private static partial Regex Reference();

    [GeneratedRegex("§[A-Za-z!]")]
    private static partial Regex ColourCode();
}
