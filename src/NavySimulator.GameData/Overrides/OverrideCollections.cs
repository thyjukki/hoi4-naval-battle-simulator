using NavySimulator.GameData.Contracts;

namespace NavySimulator.GameData.Overrides;

/// <summary>
/// The imported collections override sets can change. Designs, fleets and scenarios are the user's own and edited
/// directly; admirals are copied into each scenario side, so their traits are changed through <c>leader-traits</c>.
/// </summary>
public static class OverrideCollections
{
    public static IReadOnlyList<OverrideCollection> All { get; } =
    [
        new("hulls", "Hulls", typeof(HullDto), catalog => catalog.Hulls),
        new("modules", "Modules", typeof(ModuleDto), catalog => catalog.Modules),
        new("ship-types", "Ship types", typeof(ShipTypeDto), catalog => catalog.ShipTypes),
        new("critical-parts", "Critical parts", typeof(CriticalPartDto), catalog => catalog.CriticalParts),
        new("researches", "Researches", typeof(ResearchDto), catalog => catalog.Researches),
        new("doctrines", "Doctrines", typeof(DoctrineFolderDto), catalog => catalog.DoctrineFolders),
        new("spirits", "Spirits", typeof(SpiritDto), catalog => catalog.Spirits),
        new("mios", "MIOs", typeof(MioDto), catalog => catalog.Mios),
        new("mio-policies", "MIO policies", typeof(MioPolicyDto), catalog => catalog.MioPolicies),
        new("leader-traits", "Admiral traits", typeof(LeaderTraitDto), catalog => catalog.LeaderTraits),
        new("leader-skills", "Admiral skills", typeof(LeaderSkillDto), catalog => catalog.LeaderSkills),
        new("planes", "Planes", typeof(PlaneDto), catalog => catalog.Planes),
        new("aces", "Aces", typeof(AceTemplateDto), catalog => catalog.Aces),
        new("naval-terrains", "Naval terrains", typeof(NavalTerrainDto), catalog => catalog.NavalTerrains),
        new("nations", "Nations", typeof(NationDto), catalog => catalog.Nations)
    ];

    public static OverrideCollection? Find(string name)
    {
        return All.FirstOrDefault(collection => string.Equals(collection.Name, name, StringComparison.OrdinalIgnoreCase));
    }
}
