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
    public List<CriticalPartDto> CriticalParts { get; } = [];
    /// <summary>S-MIO: the MIOs of <c>mios/</c> (imported by <c>NavySimulator import-mios</c>).</summary>
    public List<MioDto> Mios { get; } = [];

    /// <summary>S-MIO-POLICY: the MIO policies of <c>mio-policies/</c>, empty when the folder does not exist.</summary>
    public List<MioPolicyDto> MioPolicies { get; } = [];

    /// <summary>S-MIO-MATCH: the equipment groups of <c>equipment-groups/</c>, empty when the folder does not exist.</summary>
    public List<EquipmentGroupDto> EquipmentGroups { get; } = [];

    /// <summary>S-MIO-COUNTRY: the country tags of <c>countries/</c>, empty when the folder does not exist.</summary>
    public List<CountryDto> Countries { get; } = [];
    public List<ShipDesignDto> ShipDesigns { get; } = [];
    public List<ResearchDto> Researches { get; } = [];
    public List<SpiritDto> Spirits { get; } = [];
    public List<PlaneDto> Planes { get; } = [];

    /// <summary>A-ACE: air ace templates (<c>aces/</c>, imported with the planes).</summary>
    public List<AceTemplateDto> Aces { get; } = [];
    public List<FleetDto> Fleets { get; } = [];

    /// <summary>S-ADMIRAL: the admirals of <c>admirals/</c> (imported by <c>NavySimulator import-leaders</c>), empty when the folder does not exist.</summary>
    public List<AdmiralDto> Admirals { get; } = [];

    /// <summary>S-ADMIRAL-TRAIT: the leader traits of <c>leader-traits/</c>, empty when the folder does not exist.</summary>
    public List<LeaderTraitDto> LeaderTraits { get; } = [];

    /// <summary>S-ADMIRAL-SKILL: the navy skill levels of <c>leader-skills/</c>, empty when the folder does not exist.</summary>
    public List<LeaderSkillDto> LeaderSkills { get; } = [];

    /// <summary>S-REGION: the sea regions of <c>sea-regions/</c> (imported by <c>NavySimulator import-regions</c>), empty when the folder does not exist.</summary>
    public List<SeaRegionDto> SeaRegions { get; } = [];

    /// <summary>S-TERRAIN: the naval terrains of <c>naval-terrains/</c>, empty when the folder does not exist.</summary>
    public List<NavalTerrainDto> NavalTerrains { get; } = [];

    /// <summary>S-DOCTRINE: the doctrine folders of <c>doctrines/</c> (imported), empty when the folder does not exist.</summary>
    public List<DoctrineFolderDto> DoctrineFolders { get; } = [];

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
