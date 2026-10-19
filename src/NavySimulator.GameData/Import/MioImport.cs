using NavySimulator.GameData.Contracts;

namespace NavySimulator.GameData.Import;

/// <summary>Naval MIOs, MIO policies and equipment groups read from the game's files by <see cref="MioImporter"/>.</summary>
public sealed class MioImport
{
    /// <summary>The MIOs with a naval equipment type, each with the game file it is defined in (relative path).</summary>
    public List<(MioDto Mio, string File)> Mios { get; } = [];

    /// <summary>The policies a naval MIO can take.</summary>
    public List<MioPolicyDto> Policies { get; } = [];

    /// <summary>The equipment groups that hold naval equipment.</summary>
    public List<EquipmentGroupDto> EquipmentGroups { get; } = [];

    /// <summary>The country tags (not the dynamic ones), with their names.</summary>
    public List<CountryDto> Countries { get; } = [];

    /// <summary>Script problems: unreadable files, duplicate definitions, unknown includes or traits.</summary>
    public List<string> Warnings { get; } = [];
}
