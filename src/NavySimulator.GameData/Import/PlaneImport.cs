using NavySimulator.GameData.Contracts;

namespace NavySimulator.GameData.Import;

/// <summary>The plane equipment read from the game's files by <see cref="PlaneImporter"/>.</summary>
public sealed class PlaneImport
{
    public List<PlaneDto> Planes { get; } = [];

    /// <summary>A-ACE: the air ace templates of <c>common/aces</c>.</summary>
    public List<AceTemplateDto> Aces { get; } = [];

    /// <summary>Script problems: unreadable files, duplicate definitions.</summary>
    public List<string> Warnings { get; } = [];
}
