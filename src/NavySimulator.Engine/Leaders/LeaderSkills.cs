using NavySimulator.Engine.Stats;

namespace NavySimulator.Engine.Leaders;

/// <summary>
/// S-ADMIRAL-SKILL: the navy levels of the leader skills (<c>common/unit_leader/00_*skills.txt</c>), each level's
/// <c>modifier</c> block by skill key. Spec: docs/mechanics/surface.md.
/// </summary>
public sealed class LeaderSkills(IReadOnlyDictionary<string, IReadOnlyDictionary<int, ModifierSet>> levels)
{
    public const string Skill = "skill";
    public const string Attack = "attack_skill";
    public const string Defense = "defense_skill";
    public const string Maneuvering = "maneuvering_skill";
    public const string Coordination = "coordination_skill";

    /// <summary>The skills an admiral has, overall first.</summary>
    public static IReadOnlyList<string> NavySkills { get; } = [Skill, Attack, Defense, Maneuvering, Coordination];

    public static LeaderSkills None { get; } = new(new Dictionary<string, IReadOnlyDictionary<int, ModifierSet>>());

    public IReadOnlyDictionary<string, IReadOnlyDictionary<int, ModifierSet>> Levels { get; } = levels;

    /// <summary>The highest level the files define for the skill; 0 when none.</summary>
    public int MaxLevel(string skill)
    {
        return Levels.TryGetValue(skill, out var byLevel) && byLevel.Count > 0 ? byLevel.Keys.Max() : 0;
    }

    /// <summary>
    /// S-ADMIRAL-SKILL [RE] 0xc24870, 0xae2e50: the <c>modifier</c> block of the skill at the level. Attack, defense,
    /// maneuvering and coordination read <c>clamp(level, 1, highest defined level)</c>; the overall skill reads its level
    /// only when the files define it (0xc271b0, no clamp). A level the files skip gives nothing.
    /// </summary>
    public ModifierSet Modifiers(string skill, int level)
    {
        if (!Levels.TryGetValue(skill, out var byLevel) || byLevel.Count == 0)
        {
            return ModifierSet.Empty;
        }

        var read = skill == Skill ? level : Math.Clamp(level, 1, byLevel.Keys.Max());
        return byLevel.GetValueOrDefault(read) ?? ModifierSet.Empty;
    }
}
