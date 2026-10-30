namespace NavySimulator.GameData.Contracts;

/// <summary>
/// S-NATIONAL: a scenario side's national state for its <c>country</c>: start from the country's history, completed
/// focuses, national spirits added or removed by hand, and variables set by hand.
/// </summary>
public class NationalSetupDto
{
    /// <summary>S-NATIONAL-HISTORY: start with the history file's 1936 ideas, dynamic modifiers and variables.</summary>
    public bool FromHistory { get; set; } = true;

    /// <summary>The completed national focuses (IDs); they apply in the tree's order.</summary>
    public List<string> Focuses { get; set; } = [];

    /// <summary>National spirits the side has on top (events, decisions).</summary>
    public List<string> AddedSpirits { get; set; } = [];

    /// <summary>National spirits the side no longer has.</summary>
    public List<string> RemovedSpirits { get; set; } = [];

    /// <summary>Variable values that replace the history's and focuses' result, e.g. <c>JAP_navy_naval_torpedo_hit_chance_factor</c>.</summary>
    public Dictionary<string, double> Variables { get; set; } = [];
}
