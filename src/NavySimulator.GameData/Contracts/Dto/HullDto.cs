namespace NavySimulator.GameData.Contracts;

public class HullDto
{
    public string ID { get; set; } = string.Empty;

    /// <summary>The game's display name (localisation), e.g. "Improved Cruiser".</summary>
    public string? Name { get; set; }

    /// <summary>The equipment archetype, e.g. <c>ship_hull_cruiser</c>.</summary>
    public string Archetype { get; set; } = string.Empty;

    public string? ArchetypeName { get; set; }

    /// <summary>The archetype's equipment type: <c>screen_ship</c>, <c>capital_ship</c>, <c>carrier</c> or <c>submarine</c>.</summary>
    public string EquipmentType { get; set; } = string.Empty;

    public int? Year { get; set; }
    public int Manpower { get; set; }

    /// <summary>The hull equipment's <c>naval_dominance_factor</c> (S-DOMINANCE).</summary>
    public double NavalDominanceFactor { get; set; }

    public ShipStatsDto BaseStats { get; set; } = new ShipStatsDto();
    public List<ModuleSlotDto> Slots { get; set; } = [];
    public List<ModuleCountLimitDto> ModuleCountLimits { get; set; } = [];

    /// <summary>The game's default variant: slot ID → module ID.</summary>
    public Dictionary<string, string> DefaultModules { get; set; } = [];
}
