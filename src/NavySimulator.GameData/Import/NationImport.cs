using NavySimulator.GameData.Contracts;

namespace NavySimulator.GameData.Import;

/// <summary>The nations read from the game's files by <see cref="NationImporter"/>.</summary>
public sealed class NationImport
{
    public List<NationDto> Nations { get; } = [];

    /// <summary>Script problems: unreadable files, countries without a history file or focus tree.</summary>
    public List<string> Warnings { get; } = [];
}
