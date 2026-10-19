namespace NavySimulator.Engine;

/// <summary>
/// A rule the design breaks. <see cref="SlotID"/> and <see cref="ModuleID"/> name where; <see cref="Other"/> is the
/// forbidding module (ForbiddenCategory), the limit (CountLimit) or the fallback ship type (NoMatchingShipType).
/// Everything except <see cref="DesignIssueKind.UnknownSlot"/> is a module restriction a design may override.
/// </summary>
public sealed record DesignIssue(DesignIssueKind Kind, string? SlotID, string? ModuleID, string? Other, string Message)
{
    public bool IsRestriction => Kind != DesignIssueKind.UnknownSlot;
}
