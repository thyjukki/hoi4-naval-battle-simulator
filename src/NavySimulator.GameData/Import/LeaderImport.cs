using NavySimulator.GameData.Contracts;

namespace NavySimulator.GameData.Import;

/// <summary>Navy leader skills, traits and admirals read from the game's files by <see cref="LeaderImporter"/>.</summary>
public sealed class LeaderImport
{
    /// <summary>The navy levels of each skill (overall, attack, defense, maneuvering, coordination).</summary>
    public List<LeaderSkillDto> Skills { get; } = [];

    /// <summary>The traits an admiral can have.</summary>
    public List<LeaderTraitDto> Traits { get; } = [];

    /// <summary>The characters with a <c>navy_leader</c> block.</summary>
    public List<AdmiralDto> Admirals { get; } = [];

    /// <summary>Script problems: unreadable files, duplicate definitions, unknown traits.</summary>
    public List<string> Warnings { get; } = [];
}
