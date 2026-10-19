namespace NavySimulator.Engine.Doctrines;

/// <summary>A grand doctrine: the root of a doctrine folder, with a milestone per track (S-DOCTRINE).</summary>
public sealed class GrandDoctrine(
    string id,
    string folder,
    IReadOnlyList<string> tracks,
    CountryEffects effects,
    IReadOnlyDictionary<string, CountryEffects> milestones)
{
    public string ID { get; } = id;
    public string Folder { get; } = folder;

    /// <summary>The tracks it opens, in the game's order.</summary>
    public IReadOnlyList<string> Tracks { get; } = tracks;

    public CountryEffects Effects { get; } = effects;

    /// <summary>Each track's milestone, by track ID: its effects once all rewards of the track are unlocked.</summary>
    public IReadOnlyDictionary<string, CountryEffects> Milestones { get; } = milestones;
}
