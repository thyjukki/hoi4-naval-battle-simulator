namespace NavySimulator.Engine.Random;

/// <summary>
/// Serves a logged game battle's rolls, one hour at a time, in call order (S-REPLAY). Each draw must ask for the kind
/// the game rolled next; logged rolls of a skippable kind (ones the engine does not make, such as part picks) are passed
/// over. At the first mismatch the hour's <see cref="Divergence"/> is recorded and the rest of the hour draws from
/// <paramref name="fallbackSeed"/>, so the hour still completes.
/// </summary>
public sealed class ReplayRandomSource(IReadOnlySet<RollKind> skippable, int fallbackSeed = 0, IReadOnlySet<RollKind>? unlogged = null) : IRandomSource
{
    // Kinds the game draws from a generator the log does not record: served from the fallback, the log untouched.
    private readonly IReadOnlySet<RollKind> unlogged = unlogged ?? new HashSet<RollKind>();

    private readonly System.Random fallback = new(fallbackSeed);
    private IReadOnlyList<ReplayRoll> rolls = [];
    private int position;

    /// <summary>The first mismatch of the current hour, or null while the engine follows the game.</summary>
    public ReplayDivergence? Divergence { get; private set; }

    /// <summary>Logged rolls of a skippable kind passed over this hour.</summary>
    public int Skipped { get; private set; }

    /// <summary>Logged rolls consumed or skipped so far this hour.</summary>
    public int Position => position;

    /// <summary>Starts an hour: the game's rolls of that hour in call order.</summary>
    public void Load(IReadOnlyList<ReplayRoll> hourRolls)
    {
        rolls = hourRolls;
        position = 0;
        Skipped = 0;
        Divergence = null;
    }

    /// <summary>Ends an hour: records the game's leftover rolls (other than skippable ones) as a divergence.</summary>
    public void Finish()
    {
        SkipSkippable(null);

        if (Divergence is null && position < rolls.Count)
        {
            Divergence = new ReplayDivergence(position, null, rolls[position]);
        }
    }

    public double NextDouble(RollKind kind)
    {
        if (Divergence is not null || unlogged.Contains(kind))
        {
            return fallback.NextDouble();
        }

        SkipSkippable(kind);

        if (position >= rolls.Count || rolls[position].Kind != kind)
        {
            Divergence = new ReplayDivergence(position, kind, position < rolls.Count ? rolls[position] : null);
            return fallback.NextDouble();
        }

        return rolls[position++].Value;
    }

    public double NextDouble()
    {
        return NextDouble(RollKind.Unspecified);
    }

    public int Next(int maxExclusive)
    {
        return (int)(NextDouble(RollKind.Unspecified) * maxExclusive);
    }

    private void SkipSkippable(RollKind? requested)
    {
        while (position < rolls.Count && rolls[position].Kind != requested && skippable.Contains(rolls[position].Kind))
        {
            position++;
            Skipped++;
        }
    }
}
