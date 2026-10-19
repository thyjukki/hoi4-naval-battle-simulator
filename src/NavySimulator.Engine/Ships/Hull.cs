using NavySimulator.Engine.Stats;

namespace NavySimulator.Engine;

public class Hull
{
    public string ID;

    /// <summary>The equipment archetype, e.g. <c>ship_hull_cruiser</c>. Ship types name archetypes in their <c>need</c>.</summary>
    public string Archetype;

    /// <summary>The archetype's equipment <c>type</c>, e.g. <c>screen_ship</c> (S-DESIGN-TYPE).</summary>
    public string EquipmentType;

    public int Manpower;
    public ShipStats BaseStats;
    public List<ModuleSlot> Slots;
    public List<ModuleCountLimit> ModuleCountLimits;

    public Hull(
        string id,
        string archetype,
        string equipmentType,
        int manpower,
        ShipStats baseStats,
        List<ModuleSlot>? slots = null,
        List<ModuleCountLimit>? moduleCountLimits = null)
    {
        ID = id;
        Archetype = archetype;
        EquipmentType = equipmentType;
        Manpower = manpower;
        BaseStats = baseStats;
        Slots = slots ?? [];
        ModuleCountLimits = moduleCountLimits ?? [];
    }
}
