namespace NavySimulator.GameData.Contracts;

public class ResearchDto
{
    public string ID { get; set; } = string.Empty;
    public ShipStatsDto StatModifiers { get; set; } = new();
    public ShipStatsDto StatAverages { get; set; } = new();
    public ShipStatsDto StatMultipliers { get; set; } = new();
    public List<string> AppliesToRoles { get; set; } = [];
    public List<string> AppliesToTypes { get; set; } = [];
    public bool EquipmentBonus { get; set; }

    /// <summary>S-STATS-COUNTRY: country modifiers by game key (e.g. <c>naval_retreat_speed</c>), for every ship of the side.</summary>
    public Dictionary<string, double> CountryModifiers { get; set; } = [];
}

