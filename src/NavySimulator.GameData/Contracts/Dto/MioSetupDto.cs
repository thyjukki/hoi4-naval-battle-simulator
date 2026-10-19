namespace NavySimulator.GameData.Contracts;

/// <summary>A side's state of one MIO: the unlocked traits and the attached policy (S-MIO).</summary>
public class MioSetupDto
{
    /// <summary>Unlocked trait IDs (the initial trait is always unlocked).</summary>
    public List<string> Traits { get; set; } = [];

    public string? Policy { get; set; }
}
