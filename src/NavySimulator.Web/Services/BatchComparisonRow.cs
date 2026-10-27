namespace NavySimulator.Web.Services;

/// <summary>
/// One measure of a batch run with and without an override set: the means (or shares) and the standard error of their
/// difference. <see cref="Significant"/>: the difference is more than two standard errors.
/// </summary>
public sealed record BatchComparisonRow(string Label, string? Side, double Without, double With, double StandardError, string Format)
{
    // Sums of the same doubles in another order differ in the last bits; that is no change.
    public double Change => Math.Abs(With - Without) <= 1e-9 * Math.Max(1, Math.Abs(Without)) ? 0 : With - Without;

    public bool Significant => Math.Abs(Change) > 2 * StandardError && Change != 0;
}
