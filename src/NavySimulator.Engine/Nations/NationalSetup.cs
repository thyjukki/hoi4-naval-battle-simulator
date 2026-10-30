namespace NavySimulator.Engine.Nations;

/// <summary>
/// S-NATIONAL: a side's national state: whether it starts from the country's history, the completed focuses, national
/// spirits added or removed by hand (events, decisions) and variables set by hand. Resolved by <see cref="NationalRules"/>.
/// </summary>
public sealed class NationalSetup(
    Nation nation,
    bool fromHistory,
    IReadOnlyList<NationalFocus> focuses,
    IReadOnlyList<string> addedSpirits,
    IReadOnlyList<string> removedSpirits,
    IReadOnlyDictionary<string, double> variables)
{
    public Nation Nation { get; } = nation;
    public bool FromHistory { get; } = fromHistory;

    /// <summary>The completed focuses; they apply in the tree's order.</summary>
    public IReadOnlyList<NationalFocus> Focuses { get; } = focuses;

    public IReadOnlyList<string> AddedSpirits { get; } = addedSpirits;
    public IReadOnlyList<string> RemovedSpirits { get; } = removedSpirits;

    /// <summary>Variable values that replace the history's and focuses' result.</summary>
    public IReadOnlyDictionary<string, double> Variables { get; } = variables;
}
