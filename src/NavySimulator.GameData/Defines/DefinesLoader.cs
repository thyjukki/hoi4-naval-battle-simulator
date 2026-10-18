using NavySimulator.Engine.Defines;
using NavySimulator.GameData.Paradox;

namespace NavySimulator.GameData.Defines;

/// <summary>
/// Builds <see cref="GameDefines"/> from the built-in vanilla defaults plus a list of sources, applied in order (later
/// sources override earlier ones, as mods override the game). A source is a single <c>.lua</c> file, a folder of
/// <c>.lua</c> files, or a game/mod root directory. For a root, every <c>common/defines/*.lua</c> file is applied in
/// ordinal file-name order, and the static modifier blocks naval combat uses are read from <c>common/modifiers/*.txt</c>.
/// </summary>
public static class DefinesLoader
{
    public static (GameDefines Defines, DefinesBindingReport Report) Load(IEnumerable<string> sources)
    {
        var table = new DefinesTable();
        var defines = GameDefines.Vanilla();
        var modifierWarnings = new List<string>();

        foreach (var source in sources)
        {
            var (luaFiles, modifierFiles) = ResolveSource(source);

            foreach (var file in luaFiles)
            {
                table.Merge(LuaDefinesParser.ParseFile(file));
            }

            foreach (var file in modifierFiles)
            {
                ApplyStaticModifiers(file, defines.StaticModifiers, modifierWarnings);
            }

            defines.Sources.Add(source);
        }

        var report = DefinesBinder.Apply(table, defines);
        report.ParseWarnings.AddRange(table.Warnings);
        report.ParseWarnings.AddRange(modifierWarnings);
        return (defines, report);
    }

    public static IReadOnlyList<string> ResolveFiles(string source)
    {
        return ResolveSource(source).LuaFiles;
    }

    private static (IReadOnlyList<string> LuaFiles, IReadOnlyList<string> ModifierFiles) ResolveSource(string source)
    {
        if (File.Exists(source))
        {
            return ([source], []);
        }

        if (!Directory.Exists(source))
        {
            throw new FileNotFoundException($"Defines source '{source}' was not found (expected a .lua file or a game/mod directory).");
        }

        var isRoot = Directory.Exists(Path.Combine(source, "common"));
        var definesDirectory = isRoot ? Path.Combine(source, "common", "defines") : source;
        var luaFiles = SortedFiles(definesDirectory, "*.lua");
        var modifierFiles = isRoot ? SortedFiles(Path.Combine(source, "common", "modifiers"), "*.txt") : [];

        if (luaFiles.Count == 0 && modifierFiles.Count == 0)
        {
            throw new FileNotFoundException(
                $"Defines source '{source}' contains no .lua files (expected a .lua file, a defines folder, or a game/mod root with common/defines).");
        }

        return (luaFiles, modifierFiles);
    }

    private static List<string> SortedFiles(string directory, string pattern)
    {
        return Directory.Exists(directory)
            ? Directory.GetFiles(directory, pattern).OrderBy(path => Path.GetFileName(path), StringComparer.Ordinal).ToList()
            : [];
    }

    // A block found in a later file replaces the earlier block completely, like the game's script overrides.
    private static void ApplyStaticModifiers(string file, StaticModifiers modifiers, List<string> warnings)
    {
        PdxBlock root;

        try
        {
            root = PdxParser.ParseFile(file);
        }
        catch (PdxParseException exception)
        {
            warnings.Add($"static modifiers: {exception.Message}");
            return;
        }

        foreach (var name in StaticModifiers.UsedBlocks)
        {
            var block = root.GetBlock(name);

            if (block is not null)
            {
                modifiers.Blocks[name] = block.GetNumericEntries();
            }
        }
    }
}
