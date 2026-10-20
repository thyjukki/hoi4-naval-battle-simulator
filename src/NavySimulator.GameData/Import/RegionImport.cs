using NavySimulator.GameData.Contracts;

namespace NavySimulator.GameData.Import;

/// <summary>Sea regions and naval terrains read from the game's files by <see cref="RegionImporter"/>.</summary>
public sealed class RegionImport
{
    /// <summary>The strategic regions with sea provinces.</summary>
    public List<SeaRegionDto> Regions { get; } = [];

    /// <summary>A-COVERAGE: every state's air base position (<c>map/buildings.txt</c>).</summary>
    public List<AirBaseDto> AirBases { get; } = [];

    /// <summary>The terrains with <c>naval_terrain = yes</c>.</summary>
    public List<NavalTerrainDto> Terrains { get; } = [];

    /// <summary>Script problems: unreadable files, unknown provinces, duplicate regions.</summary>
    public List<string> Warnings { get; } = [];
}
