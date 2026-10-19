namespace NavySimulator.Engine.Doctrines;

/// <summary>A subdoctrine: slotted into a track, its rewards unlock with the track's mastery (S-DOCTRINE).</summary>
public sealed class Subdoctrine(
    string id,
    string folder,
    IReadOnlyList<string> tracks,
    bool allowInMultipleTracks,
    IReadOnlyList<string> xor,
    CountryEffects effects,
    IReadOnlyList<DoctrineReward> rewards)
{
    public string ID { get; } = id;
    public string Folder { get; } = folder;

    /// <summary>The tracks it can be slotted into.</summary>
    public IReadOnlyList<string> Tracks { get; } = tracks;

    public bool AllowInMultipleTracks { get; } = allowInMultipleTracks;

    /// <summary>Subdoctrines that cannot be active in another track of the folder at the same time.</summary>
    public IReadOnlyList<string> Xor { get; } = xor;

    public CountryEffects Effects { get; } = effects;

    /// <summary>The rewards, in unlock order.</summary>
    public IReadOnlyList<DoctrineReward> Rewards { get; } = rewards;
}
