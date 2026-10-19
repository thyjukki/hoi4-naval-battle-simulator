namespace NavySimulator.Engine;

/// <summary>
/// The game's ship designer rules (spec S-DESIGN in docs/mechanics/surface.md): which modules fit which slots, which
/// modules exclude each other, module count limits, and which ship type a hull and its modules make.
/// </summary>
public static class DesignRules
{
    /// <summary>Checks a design given as slot ID → module. Slots missing from the map are empty.</summary>
    public static DesignCheck Check(Hull hull, IReadOnlyDictionary<string, StatModule> modulesBySlot, IEnumerable<ShipType> shipTypes)
    {
        var issues = new List<DesignIssue>();
        var slots = hull.Slots.ToDictionary(slot => slot.ID, StringComparer.Ordinal);

        // S-DESIGN-SLOT [PDX]: a slot takes modules of its allowed categories; required slots must be filled.
        foreach (var (slotId, module) in modulesBySlot)
        {
            if (!slots.TryGetValue(slotId, out var slot))
            {
                issues.Add(new DesignIssue(DesignIssueKind.UnknownSlot, slotId, module.ID, null,
                    $"hull '{hull.ID}' has no slot '{slotId}' (module '{module.ID}')"));
            }
            else if (!slot.Allows(module))
            {
                issues.Add(new DesignIssue(DesignIssueKind.CategoryNotAllowed, slotId, module.ID, null,
                    $"slot '{slotId}' does not take '{module.ID}' (category '{module.Category}')"));
            }
        }

        foreach (var slot in hull.Slots.Where(slot => slot.Required && !modulesBySlot.ContainsKey(slot.ID)))
        {
            issues.Add(new DesignIssue(DesignIssueKind.RequiredSlotEmpty, slot.ID, null, null, $"required slot '{slot.ID}' is empty"));
        }

        // S-DESIGN-FORBID [PDX]: a module's forbid_module_categories excludes those categories from every other slot.
        foreach (var (slotId, module) in modulesBySlot)
        {
            foreach (var forbidder in modulesBySlot
                         .Where(entry => entry.Key != slotId && entry.Value.ForbiddenCategories.Contains(module.Category, StringComparer.Ordinal))
                         .Select(entry => entry.Value.ID)
                         .Distinct(StringComparer.Ordinal))
            {
                issues.Add(new DesignIssue(DesignIssueKind.ForbiddenCategory, slotId, module.ID, forbidder,
                    $"'{forbidder}' forbids '{module.ID}' (category '{module.Category}') in slot '{slotId}'"));
            }
        }

        // S-DESIGN-LIMIT [PDX]: counted over the hull's slots, an empty slot counting as "empty".
        foreach (var limit in hull.ModuleCountLimits)
        {
            var count = hull.Slots.Count(slot => limit.Matches(modulesBySlot.GetValueOrDefault(slot.ID)));

            if (!limit.Allows(count))
            {
                issues.Add(new DesignIssue(DesignIssueKind.CountLimit, null, null, limit.ToString(),
                    $"module count limit broken ({limit}; the design has {count})"));
            }
        }

        var (shipType, matches) = ResolveShipType(hull, modulesBySlot.Values, shipTypes);

        if (shipType is not null && !matches)
        {
            issues.Add(new DesignIssue(DesignIssueKind.NoMatchingShipType, null, null, shipType.ID,
                $"no ship type accepts hull '{hull.ID}' with these modules; simulated as '{shipType.ID}'"));
        }

        return new DesignCheck(shipType, matches, issues);
    }

    /// <summary>
    /// S-DESIGN-TYPE [PDX + ASSUMED]: among the ship types that need the hull's archetype (or the hull), keep those whose
    /// <c>need_equipment_modules</c> the design meets (one of the listed modules is fitted) and whose unit types are all
    /// among the design's equipment types (the archetype's <c>type</c> plus every module's <c>add_equipment_type</c>);
    /// the highest <c>priority</c> wins. So a vanilla cruiser with a medium battery (adds <c>capital_ship</c>) is a heavy
    /// cruiser, and TFB tells light from heavy cruisers by the armor module. When none qualifies, the fallback is the
    /// highest-priority type whose unit types match, else of any type that needs the hull (<c>Matches = false</c>).
    /// </summary>
    public static (ShipType? Type, bool Matches) ResolveShipType(Hull hull, IEnumerable<StatModule> modules, IEnumerable<ShipType> shipTypes)
    {
        var fitted = modules.ToList();
        var equipmentTypes = fitted.SelectMany(module => module.AddedEquipmentTypes).Append(hull.EquipmentType).ToHashSet(StringComparer.Ordinal);
        var candidates = shipTypes.Where(type => type.Needs(hull))
            .OrderByDescending(type => type.Priority)
            .ThenBy(type => type.ID, StringComparer.Ordinal)
            .ToList();
        var typeMatches = candidates.Where(type => type.UnitTypes.All(equipmentTypes.Contains)).ToList();
        var match = typeMatches.FirstOrDefault(type =>
            type.RequiredModules.Count == 0 || fitted.Any(module => type.RequiredModules.Contains(module.ID, StringComparer.Ordinal)));

        return match is not null ? (match, true) : (typeMatches.FirstOrDefault() ?? candidates.FirstOrDefault(), false);
    }
}
