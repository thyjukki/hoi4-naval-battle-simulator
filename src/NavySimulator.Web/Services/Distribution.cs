namespace NavySimulator.Web.Services;

/// <summary>Summary statistics of a sample, for the Batch page.</summary>
public sealed record Distribution(int Count, double Mean, double Median, double Min, double Max, double P10, double P90)
{
    public static Distribution Of(IEnumerable<double> values)
    {
        var sorted = values.OrderBy(value => value).ToList();

        if (sorted.Count == 0)
        {
            return new Distribution(0, 0, 0, 0, 0, 0, 0);
        }

        return new Distribution(sorted.Count, sorted.Average(), Percentile(sorted, 0.5), sorted[0], sorted[^1], Percentile(sorted, 0.1), Percentile(sorted, 0.9));
    }

    /// <summary>Linear interpolation between closest ranks; <paramref name="sorted"/> must be ascending.</summary>
    public static double Percentile(IReadOnlyList<double> sorted, double fraction)
    {
        if (sorted.Count == 1)
        {
            return sorted[0];
        }

        var position = fraction * (sorted.Count - 1);
        var lower = (int)Math.Floor(position);
        var upper = Math.Min(lower + 1, sorted.Count - 1);
        return sorted[lower] + (sorted[upper] - sorted[lower]) * (position - lower);
    }
}
