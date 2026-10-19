using NavySimulator.GameData.Paradox;

namespace NavySimulator.GameData.Import;

/// <summary>
/// The game's files as the game sees them with mods loaded [PDX]: a mod file replaces the game (or an earlier mod) file
/// with the same path, and a mod's <c>replace_path</c> (in its <c>descriptor.mod</c>) hides every earlier file of that
/// folder. Roots are the game folder followed by the mod folders in load order.
/// </summary>
public sealed class GameFileSystem
{
    private readonly List<(string Root, HashSet<string> ReplacePaths)> roots = [];

    public GameFileSystem(string gameDirectory, IEnumerable<string>? modDirectories = null)
    {
        AddRoot(gameDirectory, isMod: false);

        foreach (var mod in modDirectories ?? [])
        {
            AddRoot(mod, isMod: true);
        }
    }

    public IReadOnlyList<string> Roots => roots.Select(root => root.Root).ToList();

    /// <summary>
    /// The effective files of a folder (relative, forward slashes) matching the pattern, as full paths, in the game's
    /// load order (ordinal by relative path). With <paramref name="recursive"/>, subfolders are included.
    /// </summary>
    public IReadOnlyList<string> GetFiles(string relativeDirectory, string searchPattern, bool recursive = false)
    {
        var folder = Normalize(relativeDirectory);
        var files = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        foreach (var (root, replacePaths) in roots)
        {
            foreach (var replaced in files.Keys.Where(path => replacePaths.Contains(DirectoryOf(path))).ToList())
            {
                files.Remove(replaced);
            }

            var directory = Path.Combine(root, folder);

            if (!Directory.Exists(directory))
            {
                continue;
            }

            foreach (var path in Directory.GetFiles(directory, searchPattern, recursive ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly))
            {
                files[Normalize(Path.GetRelativePath(root, path))] = path;
            }
        }

        return files.OrderBy(entry => entry.Key, StringComparer.OrdinalIgnoreCase).Select(entry => entry.Value).ToList();
    }

    /// <summary>The root-relative path of a file returned by <see cref="GetFiles"/>.</summary>
    public string RelativePath(string fullPath)
    {
        var root = roots.Select(entry => entry.Root)
            .Where(candidate => Path.GetFullPath(fullPath).StartsWith(Path.GetFullPath(candidate), StringComparison.OrdinalIgnoreCase))
            .OrderByDescending(candidate => candidate.Length)
            .FirstOrDefault();
        return root is null ? fullPath : Normalize(Path.GetRelativePath(root, fullPath));
    }

    private void AddRoot(string directory, bool isMod)
    {
        if (!Directory.Exists(directory))
        {
            throw new DirectoryNotFoundException($"Folder not found: {directory}");
        }

        var replacePaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var descriptor = Path.Combine(directory, "descriptor.mod");

        if (isMod && File.Exists(descriptor))
        {
            try
            {
                foreach (var value in PdxParser.ParseFile(descriptor).GetAll("replace_path").Where(value => value.Scalar is not null))
                {
                    replacePaths.Add(Normalize(value.Scalar!));
                }
            }
            catch (PdxParseException)
            {
                // A descriptor the parser cannot read replaces nothing.
            }
        }

        roots.Add((directory, replacePaths));
    }

    private static string DirectoryOf(string relativePath)
    {
        var index = relativePath.LastIndexOf('/');
        return index < 0 ? string.Empty : relativePath[..index];
    }

    private static string Normalize(string path)
    {
        return path.Replace('\\', '/').Trim('/');
    }
}
