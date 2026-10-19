using NavySimulator.Engine;
using NavySimulator.GameData;
using NavySimulator.GameData.Contracts;

namespace NavySimulator.Web.Services;

/// <summary>One slot of a design in the editor: the hull's slot, the fitted module (if any) and the slot's issues.</summary>
public sealed record SlotRow(ModuleSlotDto Slot, string? ModuleID, ModuleDto? Module, IReadOnlyList<DesignIssue> Issues);

/// <summary>
/// The Designs page's slot editor (S-DESIGN): the slots of a design's hull with their modules and rule issues, the
/// modules a slot offers, switching hulls, the hull's default modules and readable names.
/// </summary>
public static class DesignSlots
{
    /// <summary>The hull's slots in the game's order (fixed first, then custom), and modules fitted to slots the hull lacks.</summary>
    public static IReadOnlyList<SlotRow> Rows(ContentCatalog catalog, ContentLibrary library, ShipDesignDto design)
    {
        var hull = catalog.Hulls.FirstOrDefault(candidate => candidate.ID == design.HullID);
        var issues = library.CheckDesign(design)?.Issues ?? [];
        var slots = (hull?.Slots ?? []).OrderBy(slot => IsFixed(slot) ? 0 : 1).ToList();
        var unknown = design.Modules.Keys.Where(slotId => slots.All(slot => slot.ID != slotId))
            .Select(slotId => new ModuleSlotDto { ID = slotId, Name = $"{slotId} (not on this hull)" });

        return slots.Concat(unknown)
            .Select(slot =>
            {
                var moduleId = design.Modules.GetValueOrDefault(slot.ID);
                return new SlotRow(slot, moduleId, FindModule(catalog, moduleId), issues.Where(issue => issue.SlotID == slot.ID).ToList());
            })
            .ToList();
    }

    /// <summary>Issues that belong to no slot: count limits and the ship type.</summary>
    public static IReadOnlyList<DesignIssue> DesignIssues(ContentLibrary library, ShipDesignDto design)
    {
        return library.CheckDesign(design)?.Issues.Where(issue => issue.SlotID is null).ToList() ?? [];
    }

    /// <summary>The modules a slot takes (S-DESIGN-SLOT), or every module when restrictions are overridden; by category, then file order.</summary>
    public static IReadOnlyList<ModuleDto> Options(ContentCatalog catalog, ModuleSlotDto slot, bool allowAnyModule)
    {
        return catalog.Modules
            .Where(module => allowAnyModule || slot.AllowedCategories.Contains(module.Category))
            .OrderBy(module => slot.AllowedCategories.Contains(module.Category) ? 0 : 1)
            .ThenBy(module => CategoryLabel(module), StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    public static bool Allows(ModuleSlotDto slot, ModuleDto module)
    {
        return slot.AllowedCategories.Contains(module.Category);
    }

    public static bool IsFixed(ModuleSlotDto slot)
    {
        return slot.ID.StartsWith("fixed_", StringComparison.Ordinal);
    }

    /// <summary>
    /// Puts the design on another hull. Modules stay in the slots the new hull also has (and, unless restrictions are
    /// overridden, that still take them); returns the modules that were removed.
    /// </summary>
    public static IReadOnlyList<string> ChangeHull(ContentCatalog catalog, ShipDesignDto design, string hullId)
    {
        design.HullID = hullId;
        var hull = catalog.Hulls.FirstOrDefault(candidate => candidate.ID == hullId);

        if (hull is null)
        {
            return [];
        }

        var removed = new List<string>();

        foreach (var (slotId, moduleId) in design.Modules.ToList())
        {
            var slot = hull.Slots.FirstOrDefault(candidate => candidate.ID == slotId);
            var module = FindModule(catalog, moduleId);

            if (slot is null || (!design.AllowAnyModule && module is not null && !Allows(slot, module)))
            {
                design.Modules.Remove(slotId);
                removed.Add(module is null ? moduleId : ModuleLabel(module));
            }
        }

        Normalize(catalog, design);
        return removed;
    }

    /// <summary>Replaces the modules with the hull's default variant (only modules the data has).</summary>
    public static void UseDefaults(ContentCatalog catalog, ShipDesignDto design)
    {
        var hull = catalog.Hulls.FirstOrDefault(candidate => candidate.ID == design.HullID);
        design.Modules = (hull?.DefaultModules ?? [])
            .Where(entry => FindModule(catalog, entry.Value) is not null)
            .ToDictionary(entry => entry.Key, entry => entry.Value);
        Normalize(catalog, design);
    }

    /// <summary>Fits a module to a slot, or empties it when <paramref name="moduleId"/> is empty.</summary>
    public static void SetModule(ContentCatalog catalog, ShipDesignDto design, string slotId, string? moduleId)
    {
        if (string.IsNullOrEmpty(moduleId))
        {
            design.Modules.Remove(slotId);
        }
        else
        {
            design.Modules[slotId] = moduleId;
        }

        Normalize(catalog, design);
    }

    /// <summary>Orders the modules by the hull's slot order, so saved files list them as the game does.</summary>
    public static void Normalize(ContentCatalog catalog, ShipDesignDto design)
    {
        var order = catalog.Hulls.FirstOrDefault(candidate => candidate.ID == design.HullID)?.Slots.Select(slot => slot.ID).ToList() ?? [];
        design.Modules = design.Modules
            .OrderBy(entry => order.IndexOf(entry.Key) is var index && index < 0 ? int.MaxValue : index)
            .ToDictionary(entry => entry.Key, entry => entry.Value);
    }

    /// <summary>A readable description of an issue, with the game's names.</summary>
    public static string Describe(ContentCatalog catalog, DesignIssue issue)
    {
        var module = FindModule(catalog, issue.ModuleID);
        var moduleName = module is null ? issue.ModuleID : ModuleLabel(module);

        return issue.Kind switch
        {
            DesignIssueKind.UnknownSlot => $"The hull has no such slot; {moduleName} is not counted.",
            DesignIssueKind.RequiredSlotEmpty => "Required slot is empty.",
            DesignIssueKind.CategoryNotAllowed => $"This slot does not take {CategoryLabel(module)} modules.",
            DesignIssueKind.ForbiddenCategory => $"{ModuleLabelOf(catalog, issue.Other)} forbids {CategoryLabel(module)} modules on the design.",
            DesignIssueKind.CountLimit => $"Module count limit broken: {issue.Other}.",
            DesignIssueKind.NoMatchingShipType => $"No ship type accepts this hull with these modules (the game would not build it); simulated as {ShipTypeLabel(catalog, issue.Other)}.",
            _ => issue.Message
        };
    }

    public static string ModuleLabel(ModuleDto module)
    {
        var shortName = module.ShortName ?? module.Name ?? module.ID;
        return module.Name is not null && module.Name != shortName ? $"{shortName} · {module.Name}" : shortName;
    }

    public static string ModuleLabelOf(ContentCatalog catalog, string? moduleId)
    {
        var module = FindModule(catalog, moduleId);
        return module is null ? moduleId ?? string.Empty : ModuleLabel(module);
    }

    public static string CategoryLabel(ModuleDto? module)
    {
        return module is null ? "these" : module.CategoryName ?? module.Category;
    }

    public static string SlotLabel(ModuleSlotDto slot)
    {
        return slot.Name ?? slot.ID;
    }

    public static string HullLabel(HullDto hull)
    {
        return hull.Name is null ? hull.ID : $"{hull.Name} ({hull.ID})";
    }

    public static string ShipTypeLabel(ContentCatalog catalog, string? shipTypeId)
    {
        return catalog.ShipTypes.FirstOrDefault(type => type.ID == shipTypeId)?.Name ?? shipTypeId ?? "no ship type";
    }

    private static ModuleDto? FindModule(ContentCatalog catalog, string? moduleId)
    {
        return moduleId is null ? null : catalog.Modules.FirstOrDefault(module => module.ID == moduleId);
    }
}
