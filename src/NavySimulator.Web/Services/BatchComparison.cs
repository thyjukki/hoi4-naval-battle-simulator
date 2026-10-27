using NavySimulator.Engine.Results;

namespace NavySimulator.Web.Services;

/// <summary>
/// What an override set changes in a batch: outcome shares and the Batch page's means, with the set and without it,
/// each with the standard error of the difference (the runs of the two batches taken as independent samples).
/// </summary>
public static class BatchComparison
{
    public static IReadOnlyList<BatchComparisonRow> Of(IReadOnlyList<BattleResult> with, IReadOnlyList<BattleResult> without)
    {
        var rows = new List<BatchComparisonRow>();

        if (with.Count == 0 || without.Count == 0)
        {
            return rows;
        }

        foreach (var outcome in with.Concat(without).Select(run => run.Outcome).Distinct().Order())
        {
            rows.Add(Share($"{outcome} share", null, with, without, run => run.Outcome == outcome));
        }

        rows.Add(Mean("Hours", null, with, without, run => run.HoursFought, "F1"));

        foreach (var (label, side, pick) in new (string, string, Func<BattleResult, SideResult>)[]
                 {
                     ("Attacker", "attacker", run => run.Attacker),
                     ("Defender", "defender", run => run.Defender)
                 })
        {
            rows.Add(Mean($"{label} sunk", side, with, without, run => pick(run).ShipsSunk, "F2"));
            rows.Add(Mean($"{label} escaped", side, with, without, run => pick(run).ShipsRetreated, "F2"));
            rows.Add(Mean($"{label} HP lost %", side, with, without, run => Ratio(pick(run).HpLost, pick(run).HpAtStart) * 100, "F1"));
            rows.Add(Mean($"{label} HP dealt", side, with, without, run => pick(run).HpDamageDealt, "F0"));
            rows.Add(Mean($"{label} IC lost", side, with, without, run => FleetCost.Of(run, pick(run).Label).Sunk, "N0"));
        }

        return rows;
    }

    private static BatchComparisonRow Share(string label, string? side, IReadOnlyList<BattleResult> with, IReadOnlyList<BattleResult> without, Func<BattleResult, bool> test)
    {
        var p1 = with.Count(test) / (double)with.Count;
        var p0 = without.Count(test) / (double)without.Count;
        var error = Math.Sqrt(p1 * (1 - p1) / with.Count + p0 * (1 - p0) / without.Count);
        return new BatchComparisonRow(label, side, p0, p1, error, "P1");
    }

    private static BatchComparisonRow Mean(string label, string? side, IReadOnlyList<BattleResult> with, IReadOnlyList<BattleResult> without, Func<BattleResult, double> value, string format)
    {
        var (mean1, variance1) = Moments(with.Select(value).ToList());
        var (mean0, variance0) = Moments(without.Select(value).ToList());
        var error = Math.Sqrt(variance1 / with.Count + variance0 / without.Count);
        return new BatchComparisonRow(label, side, mean0, mean1, error, format);
    }

    // Mean and sample variance (0 for a single run).
    private static (double Mean, double Variance) Moments(IReadOnlyList<double> values)
    {
        var mean = values.Average();
        var variance = values.Count > 1 ? values.Sum(value => (value - mean) * (value - mean)) / (values.Count - 1) : 0;
        return (mean, variance);
    }

    private static double Ratio(double part, double whole)
    {
        return whole <= 0 ? 0 : part / whole;
    }
}
