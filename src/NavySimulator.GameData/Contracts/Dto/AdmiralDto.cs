namespace NavySimulator.GameData.Contracts;

/// <summary>
/// S-ADMIRAL: a navy leader (admiral), with the skills and traits of a character's <c>navy_leader</c> block. The imported
/// admirals (<c>NavySimulator import-leaders</c>, <c>admirals/</c>) come from <c>common/characters</c>; a scenario side's
/// admiral has the same shape, picked from them and edited, or made up.
/// </summary>
public class AdmiralDto
{
    /// <summary>The character ID, e.g. <c>USA_chester_w_nimitz</c>. In a scenario: the character it was picked from, empty for a made-up admiral.</summary>
    public string ID { get; set; } = string.Empty;

    /// <summary>The game's name (localisation), e.g. "Chester W. Nimitz".</summary>
    public string? Name { get; set; }

    /// <summary>The country tag that recruits the character, e.g. <c>USA</c>; null when the import could not tell.</summary>
    public string? Country { get; set; }

    /// <summary>The overall skill level (<c>skill</c>).</summary>
    public int Skill { get; set; } = 1;

    public int AttackSkill { get; set; } = 1;
    public int DefenseSkill { get; set; } = 1;
    public int ManeuveringSkill { get; set; } = 1;
    public int CoordinationSkill { get; set; } = 1;

    /// <summary>The leader trait IDs, e.g. <c>superior_tactician</c>.</summary>
    public List<string> Traits { get; set; } = [];
}
