namespace NavySimulator.Engine.Doctrines;

/// <summary>A side's doctrines in one folder: the grand doctrine and the slotted tracks (S-DOCTRINE).</summary>
public sealed class DoctrineSetup(GrandDoctrine grandDoctrine, IReadOnlyList<TrackSetup> tracks)
{
    public string Folder => GrandDoctrine.Folder;
    public GrandDoctrine GrandDoctrine { get; } = grandDoctrine;
    public IReadOnlyList<TrackSetup> Tracks { get; } = tracks;
}
