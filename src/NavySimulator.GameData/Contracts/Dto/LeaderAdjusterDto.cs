namespace NavySimulator.GameData.Contracts;

/// <summary>A leader trait's terrain, night or sub-unit adjuster: <c>attack</c>, <c>defence</c> and <c>movement</c> factors.</summary>
public class LeaderAdjusterDto
{
    public double Attack { get; set; }
    public double Defence { get; set; }
    public double Movement { get; set; }
}
