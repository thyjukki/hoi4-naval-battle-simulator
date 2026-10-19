namespace NavySimulator.Engine;

public enum DesignIssueKind
{
    /// <summary>The design fits a module into a slot the hull does not have.</summary>
    UnknownSlot,

    /// <summary>A required slot is empty (S-DESIGN-SLOT).</summary>
    RequiredSlotEmpty,

    /// <summary>The slot does not allow the module's category (S-DESIGN-SLOT).</summary>
    CategoryNotAllowed,

    /// <summary>Another fitted module forbids this module's category (S-DESIGN-FORBID).</summary>
    ForbiddenCategory,

    /// <summary>A hull's module count limit is broken (S-DESIGN-LIMIT).</summary>
    CountLimit,

    /// <summary>No ship type accepts the hull and modules; a fallback type is used (S-DESIGN-TYPE).</summary>
    NoMatchingShipType
}
