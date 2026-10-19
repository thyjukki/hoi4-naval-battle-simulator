namespace NavySimulator.Engine;

/// <summary>
/// The result of <see cref="DesignRules.Check"/>: the design's ship type (null when no type needs the hull at all),
/// whether that type accepts the design or is only a fallback, and the rules it breaks.
/// </summary>
public sealed record DesignCheck(ShipType? ShipType, bool ShipTypeMatches, IReadOnlyList<DesignIssue> Issues);
