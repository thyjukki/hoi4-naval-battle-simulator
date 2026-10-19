namespace NavySimulator.GameData.Contracts;

/// <summary>The subdoctrine slotted in a track and the track's mastery (S-DOCTRINE-MASTERY).</summary>
public class TrackSetupDto
{
    public string Subdoctrine { get; set; } = string.Empty;
    public double Mastery { get; set; }
}
