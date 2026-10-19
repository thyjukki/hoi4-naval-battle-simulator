namespace NavySimulator.GameData.Contracts;

/// <summary>
/// S-ADMIRAL-SKILL: one navy leader skill's levels from <c>common/unit_leader/00_*skills.txt</c> (<c>type = navy</c>),
/// written by <c>NavySimulator import-leaders</c> to <c>leader-skills/</c>.
/// </summary>
public class LeaderSkillDto
{
    /// <summary>
    /// The skill by the character key it is read with: <c>skill</c> (overall), <c>attack_skill</c>, <c>defense_skill</c>,
    /// <c>maneuvering_skill</c> or <c>coordination_skill</c>.
    /// </summary>
    public string ID { get; set; } = string.Empty;

    public List<LeaderSkillLevelDto> Levels { get; set; } = [];
}
