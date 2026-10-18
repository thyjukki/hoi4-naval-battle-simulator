using NavySimulator.Engine;
using NavySimulator.GameData.Contracts;

namespace NavySimulator.GameData;

/// <summary>
/// Engine objects for the content of a <see cref="ContentCatalog"/>, keyed by ID. Lenient: entries the engine cannot
/// represent (unknown role) are skipped and the first of duplicate IDs wins, so editors can preview designs of a
/// catalog that does not validate yet. <see cref="SetupLoader"/> only builds it from validated catalogs.
/// </summary>
public sealed class ContentLibrary
{
    private ContentLibrary(
        Dictionary<string, Hull> hulls,
        Dictionary<string, StatModule> modules,
        Dictionary<string, MioBonus> mios,
        Dictionary<string, Research> researches,
        Dictionary<string, Spirit> spirits,
        Dictionary<string, PlaneEquipment> planes)
    {
        Hulls = hulls;
        Modules = modules;
        Mios = mios;
        Researches = researches;
        Spirits = spirits;
        Planes = planes;
    }

    public IReadOnlyDictionary<string, Hull> Hulls { get; }
    public IReadOnlyDictionary<string, StatModule> Modules { get; }
    public IReadOnlyDictionary<string, MioBonus> Mios { get; }
    public IReadOnlyDictionary<string, Research> Researches { get; }
    public IReadOnlyDictionary<string, Spirit> Spirits { get; }
    public IReadOnlyDictionary<string, PlaneEquipment> Planes { get; }

    public static ContentLibrary From(ContentCatalog catalog)
    {
        var hulls = new Dictionary<string, Hull>();
        var modules = new Dictionary<string, StatModule>();
        var mios = new Dictionary<string, MioBonus>();
        var researches = new Dictionary<string, Research>();
        var spirits = new Dictionary<string, Spirit>();
        var planes = new Dictionary<string, PlaneEquipment>();

        foreach (var hull in catalog.Hulls.Where(hull => !string.IsNullOrWhiteSpace(hull.ID)))
        {
            if (Enum.TryParse<ShipRole>(hull.Role, true, out var role))
            {
                hulls.TryAdd(hull.ID, new Hull(hull.ID, role, ParseTypes(hull.Types ?? []), hull.Manpower, hull.BaseStats.ToDomain()));
            }
        }

        foreach (var module in catalog.Modules.Where(module => !string.IsNullOrWhiteSpace(module.ID)))
        {
            modules.TryAdd(module.ID, new StatModule(
                module.ID,
                module.StatModifiers.ToDomain(),
                module.StatMultipliers.ToDomain(),
                module.StatAverages.ToDomain()));
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
                research.EquipmentBonus));
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

        return new ContentLibrary(hulls, modules, mios, researches, spirits, planes);
    }

    /// <summary>
    /// The design as an engine object, or null when its hull is unknown. Unknown module and MIO IDs are left out and
    /// reported in <paramref name="problems"/>.
    /// </summary>
    public ShipDesign? TryCreateDesign(ShipDesignDto design, List<string>? problems = null)
    {
        if (!Hulls.TryGetValue(design.HullID, out var hull))
        {
            problems?.Add($"unknown hull '{design.HullID}'");
            return null;
        }

        var modules = new List<StatModule>();

        foreach (var moduleId in design.ModuleIDs)
        {
            if (Modules.TryGetValue(moduleId, out var module))
            {
                modules.Add(module);
            }
            else
            {
                problems?.Add($"unknown module '{moduleId}'");
            }
        }

        MioBonus? mio = null;

        if (!string.IsNullOrWhiteSpace(design.MioID) && !Mios.TryGetValue(design.MioID, out mio))
        {
            problems?.Add($"unknown MIO '{design.MioID}'");
        }

        return new ShipDesign(design.ID, hull, modules, mio);
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
