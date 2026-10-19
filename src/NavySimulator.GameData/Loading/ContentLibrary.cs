using NavySimulator.Engine;
using NavySimulator.Engine.Stats;
using NavySimulator.GameData.Contracts;

namespace NavySimulator.GameData;

/// <summary>
/// Engine objects for the content of a <see cref="ContentCatalog"/>, keyed by ID. Lenient: entries the engine cannot
/// represent (ship types with an unknown role) are skipped and the first of duplicate IDs wins, so editors can preview
/// designs of a catalog that does not validate yet. <see cref="SetupLoader"/> only builds it from validated catalogs.
/// </summary>
public sealed class ContentLibrary
{
    private ContentLibrary(
        Dictionary<string, Hull> hulls,
        Dictionary<string, StatModule> modules,
        Dictionary<string, ShipType> shipTypes,
        Dictionary<string, MioBonus> mios,
        Dictionary<string, Research> researches,
        Dictionary<string, Spirit> spirits,
        Dictionary<string, PlaneEquipment> planes)
    {
        Hulls = hulls;
        Modules = modules;
        ShipTypes = shipTypes;
        Mios = mios;
        Researches = researches;
        Spirits = spirits;
        Planes = planes;
    }

    public IReadOnlyDictionary<string, Hull> Hulls { get; }
    public IReadOnlyDictionary<string, StatModule> Modules { get; }
    public IReadOnlyDictionary<string, ShipType> ShipTypes { get; }
    public IReadOnlyDictionary<string, MioBonus> Mios { get; }
    public IReadOnlyDictionary<string, Research> Researches { get; }
    public IReadOnlyDictionary<string, Spirit> Spirits { get; }
    public IReadOnlyDictionary<string, PlaneEquipment> Planes { get; }

    public static ContentLibrary From(ContentCatalog catalog)
    {
        var hulls = new Dictionary<string, Hull>();
        var parts = new Dictionary<string, CriticalPart>();
        var modules = new Dictionary<string, StatModule>();
        var shipTypes = new Dictionary<string, ShipType>();
        var mios = new Dictionary<string, MioBonus>();
        var researches = new Dictionary<string, Research>();
        var spirits = new Dictionary<string, Spirit>();
        var planes = new Dictionary<string, PlaneEquipment>();

        foreach (var hull in catalog.Hulls.Where(hull => !string.IsNullOrWhiteSpace(hull.ID)))
        {
            hulls.TryAdd(hull.ID, new Hull(
                hull.ID,
                hull.Archetype,
                hull.EquipmentType,
                hull.Manpower,
                hull.BaseStats.ToDomain(),
                (hull.Slots ?? []).Select((slot, index) => new ModuleSlot(slot.ID, slot.Required, slot.AllowedCategories ?? [], slot.GameOrder ?? index)).ToList(),
                (hull.ModuleCountLimits ?? [])
                    .Select(limit => new ModuleCountLimit(limit.Modules ?? [], limit.Categories ?? [], limit.Operator, limit.Count))
                    .ToList()));
        }

        foreach (var part in catalog.CriticalParts.Where(part => !string.IsNullOrWhiteSpace(part.ID)))
        {
            parts.TryAdd(part.ID, new CriticalPart(
                part.ID,
                part.Chance,
                part.StrDamageMultiplier,
                part.OrgDamageMultiplier,
                part.StrDamage,
                part.OrgDamage,
                part.BaseDamageInstance,
                part.DamageInstancePerAddedModule,
                part.MaxDamageInstance,
                part.StatPenalties.ToDomain(),
                new ModifierSet(part.Modifiers ?? [])));
        }

        // Unknown part IDs are left out (the loader warns about them).
        List<CriticalPart> Parts(IEnumerable<string>? ids) =>
            (ids ?? []).Where(parts.ContainsKey).Select(id => parts[id]).ToList();

        foreach (var module in catalog.Modules.Where(module => !string.IsNullOrWhiteSpace(module.ID)))
        {
            modules.TryAdd(module.ID, new StatModule(
                module.ID,
                module.StatModifiers.ToDomain(),
                module.StatMultipliers.ToDomain(),
                module.StatAverages.ToDomain(),
                module.Category,
                module.AddEquipmentTypes ?? [],
                module.ForbiddenCategories ?? [],
                Parts(module.CriticalParts)));
        }

        foreach (var shipType in catalog.ShipTypes.Where(shipType => !string.IsNullOrWhiteSpace(shipType.ID)))
        {
            if (Enum.TryParse<ShipRole>(shipType.Role, true, out var role))
            {
                shipTypes.TryAdd(shipType.ID, new ShipType(
                    shipType.ID,
                    role,
                    shipType.UnitTypes ?? [],
                    shipType.Priority,
                    shipType.Hulls ?? [],
                    shipType.RequiredModules ?? [],
                    shipType.Organization,
                    Parts(shipType.CriticalParts),
                    shipType.CriticalPartDamageChanceMult));
            }
        }

        foreach (var mio in catalog.Mios.Where(mio => !string.IsNullOrWhiteSpace(mio.ID)))
        {
            mios.TryAdd(mio.ID, new MioBonus(mio.ID, mio.Modifiers
                .Select(modifier => new MioModifier(
                    modifier.StatModifiers.ToDomain(),
                    modifier.StatAverages.ToDomain(),
                    modifier.StatMultipliers.ToDomain(),
                    ParseRoles(modifier.AppliesToRoles),
                    ParseTypes(modifier.AppliesToTypes ?? [])))
                .ToList()));
        }

        foreach (var research in catalog.Researches.Where(research => !string.IsNullOrWhiteSpace(research.ID)))
        {
            researches.TryAdd(research.ID, new Research(
                research.ID,
                research.StatModifiers.ToDomain(),
                research.StatAverages.ToDomain(),
                research.StatMultipliers.ToDomain(),
                ParseRoles(research.AppliesToRoles),
                ParseTypes(research.AppliesToTypes ?? []),
                research.EquipmentBonus,
                new ModifierSet(research.CountryModifiers ?? [])));
        }

        foreach (var spirit in catalog.Spirits.Where(spirit => !string.IsNullOrWhiteSpace(spirit.ID)))
        {
            spirits.TryAdd(spirit.ID, new Spirit(
                spirit.ID,
                spirit.StatModifiers.ToDomain(),
                spirit.StatAverages.ToDomain(),
                spirit.StatMultipliers.ToDomain(),
                spirit.SortieEffiency,
                ParseRoles(spirit.AppliesToRoles),
                ParseTypes(spirit.AppliesToTypes ?? [])));
        }

        foreach (var plane in catalog.Planes.Where(plane => !string.IsNullOrWhiteSpace(plane.ID)))
        {
            planes.TryAdd(plane.ID, new PlaneEquipment(plane.ID, plane.Stats.ToDomain()));
        }

        return new ContentLibrary(hulls, modules, shipTypes, mios, researches, spirits, planes);
    }

    /// <summary>
    /// The design as an engine object, or null when its hull is unknown or no ship type needs the hull. Unknown module
    /// and MIO IDs are left out and reported in <paramref name="problems"/>; so are modules in slots the hull does not
    /// have. Module restrictions do not stop a design: see <see cref="CheckDesign"/>.
    /// </summary>
    public ShipDesign? TryCreateDesign(ShipDesignDto design, List<string>? problems = null)
    {
        if (!TryResolveModules(design, problems, out var hull, out var modulesBySlot))
        {
            return null;
        }

        foreach (var slotId in modulesBySlot.Keys.Where(slotId => hull.Slots.All(slot => slot.ID != slotId)))
        {
            problems?.Add($"hull '{hull.ID}' has no slot '{slotId}'");
        }

        // In the game's slot order: stats do not depend on it, the critical part pick does (S-CRIT-PART).
        var modules = hull.Slots.Where(slot => modulesBySlot.ContainsKey(slot.ID)).OrderBy(slot => slot.GameOrder).Select(slot => modulesBySlot[slot.ID]).ToList();
        var (shipType, _) = DesignRules.ResolveShipType(hull, modules, ShipTypes.Values);

        if (shipType is null)
        {
            problems?.Add($"no ship type needs hull '{hull.ID}' (archetype '{hull.Archetype}')");
            return null;
        }

        MioBonus? mio = null;

        if (!string.IsNullOrWhiteSpace(design.MioID) && !Mios.TryGetValue(design.MioID, out mio))
        {
            problems?.Add($"unknown MIO '{design.MioID}'");
        }

        return new ShipDesign(design.ID, hull, shipType, modules, mio);
    }

    /// <summary>The designer rules for the design (S-DESIGN), or null when its hull is unknown. Unknown modules are skipped.</summary>
    public DesignCheck? CheckDesign(ShipDesignDto design)
    {
        return TryResolveModules(design, null, out var hull, out var modulesBySlot)
            ? DesignRules.Check(hull, modulesBySlot, ShipTypes.Values)
            : null;
    }

    private bool TryResolveModules(
        ShipDesignDto design,
        List<string>? problems,
        [System.Diagnostics.CodeAnalysis.NotNullWhen(true)] out Hull? hull,
        out Dictionary<string, StatModule> modulesBySlot)
    {
        modulesBySlot = new Dictionary<string, StatModule>(StringComparer.Ordinal);

        if (!Hulls.TryGetValue(design.HullID, out hull))
        {
            problems?.Add($"unknown hull '{design.HullID}'");
            return false;
        }

        foreach (var (slotId, moduleId) in design.Modules ?? [])
        {
            if (Modules.TryGetValue(moduleId, out var module))
            {
                modulesBySlot[slotId] = module;
            }
            else
            {
                problems?.Add($"unknown module '{moduleId}' in slot '{slotId}'");
            }
        }

        return true;
    }

    internal static List<ShipRole> ParseRoles(List<string> roleNames)
    {
        var roles = new List<ShipRole>();

        foreach (var roleName in roleNames)
        {
            if (Enum.TryParse<ShipRole>(roleName, true, out var role))
            {
                roles.Add(role);
            }
        }

        return roles;
    }

    internal static List<string> ParseTypes(List<string> typeNames)
    {
        return typeNames
            .Where(typeName => !string.IsNullOrWhiteSpace(typeName))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
    }
}
