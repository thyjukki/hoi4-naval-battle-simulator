using System.Globalization;
using NavySimulator.Engine.Battles;
using NavySimulator.Engine.Defines;
using NavySimulator.Engine.Leaders;
using NavySimulator.GameData.Contracts;

namespace NavySimulator.Web.Services;

/// <summary>Display helpers for admirals, their skills and traits (S-ADMIRAL).</summary>
public static class AdmiralInfo
{
    /// <summary>The skills in the game's order, with the labels its admiral window uses.</summary>
    public static IReadOnlyList<(string Key, string Label)> Skills { get; } =
    [
        (LeaderSkills.Skill, "Skill"), (LeaderSkills.Attack, "Attack"), (LeaderSkills.Defense, "Defense"),
        (LeaderSkills.Maneuvering, "Maneuvering"), (LeaderSkills.Coordination, "Coordination")
    ];

    // Trait groups in the order the editor lists them.
    private static readonly string[] GroupOrder = ["Basic", "Assignable (trait tree)", "Terrain", "Personality", "Status"];

    // Modifiers that are flat values, not factors (shown without %).
    private static readonly HashSet<string> FlatModifiers = new(["navy_org", "naval_morale", "submarine_attack", "navy_anti_air_attack"], StringComparer.Ordinal);

    public static int Level(AdmiralDto admiral, string skill)
    {
        return skill switch
        {
            LeaderSkills.Skill => admiral.Skill,
            LeaderSkills.Attack => admiral.AttackSkill,
            LeaderSkills.Defense => admiral.DefenseSkill,
            LeaderSkills.Maneuvering => admiral.ManeuveringSkill,
            LeaderSkills.Coordination => admiral.CoordinationSkill,
            _ => 0
        };
    }

    public static void SetLevel(AdmiralDto admiral, string skill, int level)
    {
        switch (skill)
        {
            case LeaderSkills.Skill: admiral.Skill = level; break;
            case LeaderSkills.Attack: admiral.AttackSkill = level; break;
            case LeaderSkills.Defense: admiral.DefenseSkill = level; break;
            case LeaderSkills.Maneuvering: admiral.ManeuveringSkill = level; break;
            case LeaderSkills.Coordination: admiral.CoordinationSkill = level; break;
        }
    }

    /// <summary>"4 · 2/2/4/5": overall skill, then attack/defense/maneuvering/coordination.</summary>
    public static string SkillLine(AdmiralDto admiral)
    {
        return string.Create(CultureInfo.InvariantCulture,
            $"{admiral.Skill} · {admiral.AttackSkill}/{admiral.DefenseSkill}/{admiral.ManeuveringSkill}/{admiral.CoordinationSkill}");
    }

    /// <summary>"Chester W. Nimitz (4 · 2/2/4/5)".</summary>
    public static string Label(AdmiralDto admiral)
    {
        return $"{Name(admiral)} ({SkillLine(admiral)})";
    }

    public static string Name(AdmiralDto admiral)
    {
        return string.IsNullOrWhiteSpace(admiral.Name) ? (string.IsNullOrWhiteSpace(admiral.ID) ? "Admiral" : admiral.ID) : admiral.Name;
    }

    /// <summary>Whether two admirals have the same name, skills and traits (a picked admiral not edited since).</summary>
    public static bool SameAs(AdmiralDto admiral, AdmiralDto other)
    {
        return Name(admiral) == Name(other) && Skills.All(skill => Level(admiral, skill.Key) == Level(other, skill.Key)) &&
               (admiral.Traits ?? []).Order(StringComparer.Ordinal).SequenceEqual((other.Traits ?? []).Order(StringComparer.Ordinal));
    }

    /// <summary>
    /// S-POTF: what a pride of the fleet gives: its static block on the ship itself, and the side-wide bonus the admiral's
    /// naval_has_potf_in_combat_* gives while it is in the battle.
    /// </summary>
    public static string PrideOfTheFleetEffects(Admiral? admiral)
    {
        var attack = admiral?.CombatModifiers.Get(StaticModifiers.NavalHasPotfInCombatAttack) ?? 0;
        var defense = admiral?.CombatModifiers.Get(StaticModifiers.NavalHasPotfInCombatDefense) ?? 0;
        var ship = "The ship takes half the gun and torpedo crits (but 1.5 times the air crits, as in the game) and gains experience 25 % faster.";
        return attack == 0 && defense == 0
            ? $"{ship} The side's ships get an attack and defense bonus only from an admiral trait (Media Personality)."
            : $"{ship} Until it sinks, the side's ships get attack {Percent(attack)} and defense {Percent(defense)} from the admiral.";
    }

    private static string Percent(double value)
    {
        return (value * 100).ToString("+0.##;-0.##", CultureInfo.InvariantCulture) + "%";
    }

    /// <summary>The editor group of a trait, from its <c>trait_type</c> (none: a basic trait).</summary>
    public static string Group(LeaderTraitDto trait)
    {
        return trait.TraitType switch
        {
            "personality_trait" => "Personality",
            "status_trait" => "Status",
            "assignable_trait" => "Assignable (trait tree)",
            "basic_terrain_trait" or "assignable_terrain_trait" => "Terrain",
            _ => "Basic"
        };
    }

    public static int GroupIndex(LeaderTraitDto trait)
    {
        var index = Array.IndexOf(GroupOrder, Group(trait));
        return index < 0 ? GroupOrder.Length : index;
    }

    /// <summary>
    /// What a trait gives, e.g. "naval_damage_factor +2.5%, destroyer: naval_speed_factor +5%, water_deep_ocean: attack +10%
    /// (naval terrain not simulated)". Keys combat does not read are marked.
    /// </summary>
    public static string Summarize(LeaderTraitDto trait)
    {
        var parts = new List<string>();
        AddIfAny(parts, Summarize(trait.Modifiers));
        AddIfAny(parts, Summarize(trait.NonSharedModifiers));

        foreach (var (subUnit, modifiers) in trait.SubUnitModifiers ?? [])
        {
            parts.Add($"{subUnit}: {Summarize(modifiers)}");
        }

        foreach (var (subUnit, adjuster) in trait.SubUnitAdjusters ?? [])
        {
            parts.Add($"{subUnit}: {Adjuster(adjuster)}");
        }

        foreach (var (key, adjuster) in trait.Adjusters ?? [])
        {
            parts.Add(key == LeaderTrait.Night ? $"night: {Adjuster(adjuster)} × darkness" : $"{key}: {Adjuster(adjuster)} (on that naval terrain)");
        }

        foreach (var (skill, add) in trait.Skills ?? [])
        {
            parts.Add($"{skill.Replace('_', ' ')} {add:+0;-0}");
        }

        if ((trait.Scripted ?? []).Count > 0)
        {
            parts.Add($"{string.Join(", ", trait.Scripted!)} (not simulated)");
        }

        return parts.Count == 0 ? "no effect" : string.Join(", ", parts);
    }

    /// <summary>
    /// An admiral's modifiers as the game shows them ("naval_damage_factor +10%"), each marked when combat does not read
    /// it from an admiral: "(no effect in battle)" or "(not simulated)".
    /// </summary>
    public static string Summarize(IReadOnlyDictionary<string, double>? modifiers)
    {
        return string.Join(", ", (modifiers ?? new Dictionary<string, double>())
            .Where(entry => entry.Value != 0)
            .Select(entry => $"{ModifierInfo.Label(entry.Key)} {Value(entry.Key, entry.Value)}{Status(entry.Key)}"));
    }

    private static string Value(string key, double value)
    {
        return FlatModifiers.Contains(key)
            ? value.ToString("+0.##;-0.##", CultureInfo.InvariantCulture)
            : (value * 100).ToString("+0.##;-0.##", CultureInfo.InvariantCulture) + "%";
    }

    // S-ADMIRAL: whether combat reads the key from an admiral.
    private static string Status(string key)
    {
        return BattleParticipant.IsModelledAdmiralModifier(key) ? string.Empty
            : BattleParticipant.AdmiralModifiersWithoutBattleEffect.Contains(key) ? " (no effect in battle)"
            : " (not simulated)";
    }

    private static string Adjuster(LeaderAdjusterDto adjuster)
    {
        var parts = new[] { ("attack", adjuster.Attack), ("defence", adjuster.Defence), ("movement", adjuster.Movement) }
            .Where(part => part.Item2 != 0)
            .Select(part => $"{part.Item1} {(part.Item2 * 100).ToString("+0.##;-0.##", CultureInfo.InvariantCulture)}%");
        return string.Join(", ", parts);
    }

    private static void AddIfAny(List<string> parts, string part)
    {
        if (part.Length > 0)
        {
            parts.Add(part);
        }
    }
}
