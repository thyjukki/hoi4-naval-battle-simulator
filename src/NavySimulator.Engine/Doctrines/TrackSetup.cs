namespace NavySimulator.Engine.Doctrines;

/// <summary>A track of a side's grand doctrine with its slotted subdoctrine and mastery (S-DOCTRINE-MASTERY).</summary>
public sealed class TrackSetup(string track, Subdoctrine subdoctrine, double mastery)
{
    public string Track { get; } = track;
    public Subdoctrine Subdoctrine { get; } = subdoctrine;
    public double Mastery { get; } = mastery;
}
