namespace NavySimulator.GameData.Contracts;

/// <summary>A leader trait's parent condition: <see cref="Needed"/> of <see cref="Traits"/> must be had first.</summary>
public class LeaderTraitParentDto
{
    public List<string> Traits { get; set; } = [];
    public int Needed { get; set; } = 1;
}
