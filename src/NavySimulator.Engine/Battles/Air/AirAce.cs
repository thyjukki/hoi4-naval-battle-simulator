namespace NavySimulator.Engine.Battles.Air;

/// <summary>A-ACE [RE]: an ace flying with one wing; its bonuses count while it is alive (every stat getter checks it).</summary>
public sealed class AirAce(AceTemplate template, int hour)
{
    public AceTemplate Template { get; } = template;

    /// <summary>Battle hour it was earned.</summary>
    public int Hour { get; } = hour;

    public bool Alive { get; set; } = true;
}
