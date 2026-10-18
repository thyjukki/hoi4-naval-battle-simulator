namespace NavySimulator.Web.Components.Shared;

/// <summary>One line of a <see cref="LineChart"/>: a value per battle hour, drawn in a CSS color.</summary>
public sealed record ChartSeries(string Name, string Color, IReadOnlyList<double> Values, bool Dashed = false);
