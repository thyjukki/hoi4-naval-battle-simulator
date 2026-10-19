using NavySimulator.GameData.Contracts;

namespace NavySimulator.GameData;

/// <summary>
/// The content of a data directory (everything except scenarios) as read from JSON, before validation. Each item
/// remembers the file it came from, so editors can write it back with <see cref="CatalogWriter"/>.
/// Build a battle from it with <see cref="SetupLoader.BuildScenario(ContentCatalog, BattleScenarioDto, IReadOnlyList{string}?)"/>.
/// </summary>
public sealed class ContentCatalog
{
    private readonly Dictionary<object, string> sourceFiles = new(ReferenceEqualityComparer.Instance);

    public ContentCatalog(string dataDirectory)
    {
        DataDirectory = dataDirectory;
    }

    public string DataDirectory { get; }

    public List<HullDto> Hulls { get; } = [];
    public List<ModuleDto> Modules { get; } = [];
    public List<ShipTypeDto> ShipTypes { get; } = [];
    public List<MioBonusDto> Mios { get; } = [];
    public List<ShipDesignDto> ShipDesigns { get; } = [];
    public List<ResearchDto> Researches { get; } = [];
    public List<SpiritDto> Spirits { get; } = [];
    public List<PlaneDto> Planes { get; } = [];
    public List<FleetDto> Fleets { get; } = [];

    /// <summary>Problems reading the files (missing folders, invalid JSON). They fail every scenario build.</summary>
    public List<string> ReadErrors { get; } = [];

    /// <summary>Ignored content, such as unknown stat keys.</summary>
    public List<string> Warnings { get; } = [];

    /// <summary>The data-directory-relative path (forward slashes) of the file the item was read from, if any.</summary>
    public string? SourceFileOf(object item)
    {
        return sourceFiles.GetValueOrDefault(item);
    }

    public void SetSourceFile(object item, string relativePath)
    {
        sourceFiles[item] = relativePath.Replace('\\', '/');
    }

    /// <summary>The files a collection folder has, e.g. <c>ship-designs/battleships.json</c>, including empty ones.</summary>
    public IReadOnlyList<string> FilesIn(string folder)
    {
        var directory = Path.Combine(DataDirectory, folder);

        if (!Directory.Exists(directory))
        {
            return [];
        }

        return Directory.GetFiles(directory, "*.json", SearchOption.TopDirectoryOnly)
            .Select(path => $"{folder}/{Path.GetFileName(path)}")
            .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }
}
