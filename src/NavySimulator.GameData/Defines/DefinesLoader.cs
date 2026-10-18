using NavySimulator.Engine.Defines;

namespace NavySimulator.GameData.Defines;

/// <summary>
/// Builds <see cref="GameDefines"/> from the built-in vanilla defaults plus a list of define sources, applied in order.
/// A source is either a single <c>.lua</c> file or a game/mod root directory, in which case every
/// <c>common/defines/*.lua</c> file is applied in ordinal file-name order (the order the game loads them).
/// </summary>
public static class DefinesLoader
{
    public static (GameDefines Defines, DefinesBindingReport Report) Load(IEnumerable<string> sources)
    {
        var table = new DefinesTable();
        var defines = GameDefines.Vanilla();

        foreach (var source in sources)
        {
            foreach (var file in ResolveFiles(source))
            {
                table.Merge(LuaDefinesParser.ParseFile(file));
            }

            defines.Sources.Add(source);
        }

        var report = DefinesBinder.Apply(table, defines);
        report.ParseWarnings.AddRange(table.Warnings);
        return (defines, report);
    }

    public static IReadOnlyList<string> ResolveFiles(string source)
    {
        if (File.Exists(source))
        {
            return [source];
        }

        var definesDirectory = Directory.Exists(Path.Combine(source, "common", "defines"))
            ? Path.Combine(source, "common", "defines")
            : source;

        if (!Directory.Exists(definesDirectory))
        {
            throw new FileNotFoundException($"Defines source '{source}' was not found (expected a .lua file or a game/mod directory).");
        }

        var files = Directory.GetFiles(definesDirectory, "*.lua")
            .OrderBy(path => Path.GetFileName(path), StringComparer.Ordinal)
            .ToList();

        if (files.Count == 0)
        {
            throw new FileNotFoundException($"Defines source '{source}' contains no .lua files (expected a .lua file, a defines folder, or a game/mod root with common/defines).");
        }

        return files;
    }
}
