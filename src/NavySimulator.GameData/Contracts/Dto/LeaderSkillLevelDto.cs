namespace NavySimulator.GameData.Contracts;

/// <summary>One level of a navy leader skill.</summary>
public class LeaderSkillLevelDto
{
    public int Level { get; set; }

    /// <summary>The <c>modifier</c> block: what an admiral with this level gives the fleet under their command.</summary>
    public Dictionary<string, double> Modifiers { get; set; } = [];

    /// <summary>
    /// The <c>building_module_modifier</c> block: what the admiral gives when assigned to a naval headquarters. Not
    /// simulated (no naval headquarters in a battle setup).
    /// </summary>
    public Dictionary<string, double> BuildingModuleModifiers { get; set; } = [];
}
