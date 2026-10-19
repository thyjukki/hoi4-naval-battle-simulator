namespace NavySimulator.Engine.Leaders;

/// <summary>
/// S-ADMIRAL-TRAIT: a leader trait's terrain, night or sub-unit adjuster (<c>attack</c>, <c>defence</c>, <c>movement</c>);
/// adjusters of several traits add up.
/// </summary>
public readonly record struct LeaderAdjuster(double Attack, double Defence, double Movement)
{
    public LeaderAdjuster Add(LeaderAdjuster other)
    {
        return new LeaderAdjuster(Attack + other.Attack, Defence + other.Defence, Movement + other.Movement);
    }
}
