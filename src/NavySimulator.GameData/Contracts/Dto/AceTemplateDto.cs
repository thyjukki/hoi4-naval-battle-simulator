namespace NavySimulator.GameData.Contracts;

/// <summary>A-ACE: an air ace template of the game's <c>common/aces</c> (imported with the planes).</summary>
public class AceTemplateDto
{
    public string ID { get; set; } = string.Empty;

    /// <summary>The plane sub-unit <c>type</c> keywords the ace can come to, e.g. <c>naval_bomber</c>.</summary>
    public List<string> Types { get; set; } = [];

    /// <summary>The template's weight among the templates a wing can get.</summary>
    public double Chance { get; set; }

    /// <summary>The ace's modifiers (<c>effect</c>), e.g. <c>naval_strike_targetting_factor</c>.</summary>
    public Dictionary<string, double> Effects { get; set; } = [];
}
