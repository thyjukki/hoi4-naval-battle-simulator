using System.Text.RegularExpressions;

namespace NavySimulator.Web.Services;

/// <summary>
/// Finds HOI4 installs and mods, so the Settings page can offer them as defines sources: the game and workshop mods in
/// Steam libraries, and every mod the launcher knows from its mod folder (<c>*.mod</c> files and their <c>path</c>,
/// which covers local checkouts). Only folders with <c>common/defines</c> or <c>common/modifiers</c> are listed,
/// since nothing else affects the battle.
/// </summary>
public static partial class Hoi4Locator
{
    private const string WorkshopAppId = "394360";

    public static IReadOnlyList<DefinesCandidate> Find()
    {
        return Find(SteamLibraries(), ParadoxModFolder());
    }

    public static IReadOnlyList<DefinesCandidate> Find(IEnumerable<string> steamLibraries, string? paradoxModFolder)
    {
        var candidates = new List<DefinesCandidate>();
        var workshopFolders = new List<string>();

        foreach (var library in steamLibraries)
        {
            var game = Path.Combine(library, "steamapps", "common", "Hearts of Iron IV");

            if (HasCombatFiles(game))
            {
                candidates.Add(new DefinesCandidate("Hearts of Iron IV", game, DefinesCandidateKind.Game));
            }

            var workshop = Path.Combine(library, "steamapps", "workshop", "content", WorkshopAppId);

            if (Directory.Exists(workshop))
            {
                workshopFolders.Add(Normalize(workshop));
                candidates.AddRange(Directory.GetDirectories(workshop)
                    .Where(HasCombatFiles)
                    .Select(mod => new DefinesCandidate(DescriptorName(mod) ?? Path.GetFileName(mod), mod, DefinesCandidateKind.WorkshopMod)));
            }
        }

        if (paradoxModFolder is not null && Directory.Exists(paradoxModFolder))
        {
            // The launcher's .mod files name the mods it shows, wherever they live; plain folders may have no .mod file.
            var launcherMods = Directory.GetFiles(paradoxModFolder, "*.mod")
                .Select(ReadModFile)
                .Where(mod => mod.Path is not null)
                .Select(mod => (mod.Name, Path: mod.Path!));
            var folderMods = Directory.GetDirectories(paradoxModFolder)
                .Select(folder => (Name: DescriptorName(folder) ?? Path.GetFileName(folder), Path: folder));

            foreach (var (name, path) in launcherMods.Concat(folderMods).Where(mod => HasCombatFiles(mod.Path)))
            {
                var kind = workshopFolders.Any(workshop => Normalize(path).StartsWith(workshop + "/", StringComparison.Ordinal))
                    ? DefinesCandidateKind.WorkshopMod
                    : DefinesCandidateKind.LocalMod;
                candidates.Add(new DefinesCandidate(name, path, kind));
            }
        }

        return candidates
            .DistinctBy(candidate => Normalize(candidate.Path))
            .OrderBy(candidate => candidate.Kind)
            .ThenBy(candidate => candidate.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    /// <summary>A readable name for a defines source: the matching candidate's, the mod descriptor's, or the file or folder name.</summary>
    public static string DisplayName(string source, IEnumerable<DefinesCandidate> candidates)
    {
        var match = candidates.FirstOrDefault(candidate => Normalize(candidate.Path) == Normalize(source));

        if (match is not null)
        {
            return match.Name;
        }

        return (Directory.Exists(source) ? DescriptorName(source) : null) ?? Path.GetFileName(source.TrimEnd('/', '\\'));
    }

    /// <summary>A path in a form that compares equal for the same folder: full, forward slashes, no trailing slash, lower case.</summary>
    public static string Normalize(string path)
    {
        try
        {
            return Path.GetFullPath(path).Replace('\\', '/').TrimEnd('/').ToLowerInvariant();
        }
        catch (Exception exception) when (exception is ArgumentException or NotSupportedException or PathTooLongException)
        {
            return path.ToLowerInvariant();
        }
    }

    /// <summary>The launcher's mod folder: Documents/Paradox Interactive/Hearts of Iron IV/mod (or the Linux equivalent).</summary>
    public static string? ParadoxModFolder()
    {
        var documents = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
        var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        return new[]
            {
                Path.Combine(documents, "Paradox Interactive", "Hearts of Iron IV", "mod"),
                Path.Combine(home, ".local", "share", "Paradox Interactive", "Hearts of Iron IV", "mod")
            }
            .FirstOrDefault(Directory.Exists);
    }

    private static IEnumerable<string> SteamLibraries()
    {
        var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        var roots = new[]
        {
            @"C:\Program Files (x86)\Steam",
            @"C:\Program Files\Steam",
            Path.Combine(home, ".steam", "steam"),
            Path.Combine(home, ".local", "share", "Steam"),
            Path.Combine(home, "Library", "Application Support", "Steam")
        };
        var libraries = new List<string>();

        foreach (var root in roots.Where(Directory.Exists))
        {
            libraries.Add(root);
            var folders = Path.Combine(root, "steamapps", "libraryfolders.vdf");

            if (File.Exists(folders))
            {
                try
                {
                    libraries.AddRange(LibraryPath().Matches(File.ReadAllText(folders))
                        .Select(match => match.Groups[1].Value.Replace(@"\\", @"\"))
                        .Where(Directory.Exists));
                }
                catch (IOException)
                {
                    // Unreadable library list: the default library is still searched.
                }
            }
        }

        return libraries.Distinct(StringComparer.OrdinalIgnoreCase);
    }

    private static bool HasCombatFiles(string folder)
    {
        return Directory.Exists(Path.Combine(folder, "common", "defines")) ||
               Directory.Exists(Path.Combine(folder, "common", "modifiers"));
    }

    // A launcher .mod file: name="..." and path="..." (absolute, or relative to the folder above "mod").
    private static (string Name, string? Path) ReadModFile(string file)
    {
        try
        {
            var text = File.ReadAllText(file);
            var name = NameLine().Match(text) is { Success: true } nameMatch ? nameMatch.Groups[1].Value.Trim() : Path.GetFileNameWithoutExtension(file);
            var path = PathLine().Match(text) is { Success: true } pathMatch ? pathMatch.Groups[1].Value : null;

            if (path is not null && !Path.IsPathRooted(path))
            {
                path = Path.Combine(Path.GetDirectoryName(Path.GetDirectoryName(file)!)!, path);
            }

            return (name, path);
        }
        catch (IOException)
        {
            return (Path.GetFileNameWithoutExtension(file), null);
        }
    }

    private static string? DescriptorName(string folder)
    {
        var descriptor = Path.Combine(folder, "descriptor.mod");

        try
        {
            return File.Exists(descriptor) && NameLine().Match(File.ReadAllText(descriptor)) is { Success: true } match ? match.Groups[1].Value.Trim() : null;
        }
        catch (IOException)
        {
            return null;
        }
    }

    [GeneratedRegex("\"path\"\\s+\"([^\"]+)\"")]
    private static partial Regex LibraryPath();

    [GeneratedRegex("^\\s*name\\s*=\\s*\"([^\"]*)\"", RegexOptions.Multiline)]
    private static partial Regex NameLine();

    [GeneratedRegex("^\\s*path\\s*=\\s*\"([^\"]*)\"", RegexOptions.Multiline)]
    private static partial Regex PathLine();
}
