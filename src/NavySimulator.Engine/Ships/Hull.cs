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

    /// <summary>
    /// S-DOMINANCE [RE] 0xbd5d60: the hull equipment's <c>naval_dominance_factor</c>, a ship's naval dominance before the
    /// speed, range, heavy attack and carrier size terms.
    /// </summary>
    public double NavalDominanceFactor;

    /// <summary>
    /// S-MIO-MATCH [RE] 0xc949d0: the <c>add_equipment_type</c> of the hull's default modules, e.g. <c>capital_ship</c> for
    /// TFB's torpedo cruiser hull. They join the hull's equipment types whatever modules a design installs.
    /// </summary>
    public IReadOnlyList<string> DefaultModuleEquipmentTypes;

    public Hull(
        string id,
        string archetype,
        string equipmentType,
        int manpower,
        ShipStats baseStats,
        List<ModuleSlot>? slots = null,
        List<ModuleCountLimit>? moduleCountLimits = null,
        double navalDominanceFactor = 0,
        IReadOnlyList<string>? defaultModuleEquipmentTypes = null)
    {
        ID = id;
        Archetype = archetype;
        EquipmentType = equipmentType;
        Manpower = manpower;
        BaseStats = baseStats;
        Slots = slots ?? [];
        ModuleCountLimits = moduleCountLimits ?? [];
        NavalDominanceFactor = navalDominanceFactor;
        DefaultModuleEquipmentTypes = defaultModuleEquipmentTypes ?? [];
    }
}
