using NavySimulator.GameData.Contracts;

namespace NavySimulator.GameData.Import;

/// <summary>Doctrine folders and navy/air spirits read from the game's files by <see cref="DoctrineImporter"/>.</summary>
public sealed class DoctrineImport
{
    public List<DoctrineFolderDto> Folders { get; } = [];
    public List<SpiritDto> Spirits { get; } = [];

    /// <summary>Script problems: unreadable files, duplicate definitions, milestones without a track.</summary>
    public List<string> Warnings { get; } = [];
}
