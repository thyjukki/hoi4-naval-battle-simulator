namespace NavySimulator.GameData.Contracts;

/// <summary>A side's doctrines in one folder: the grand doctrine and, by track ID, the slotted subdoctrines (S-DOCTRINE).</summary>
public class DoctrineSetupDto
{
    public string GrandDoctrine { get; set; } = string.Empty;
    public Dictionary<string, TrackSetupDto> Tracks { get; set; } = [];
}
