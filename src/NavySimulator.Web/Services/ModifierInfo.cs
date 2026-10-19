using NavySimulator.Engine.Battles;
using NavySimulator.GameData.Contracts;

namespace NavySimulator.Web.Services;

/// <summary>Display names for country modifiers (S-STATS-COUNTRY), as the game's localisation shows them.</summary>
public static class ModifierInfo
{
    // MODIFIER_* in localisation/english/modifiers_l_english.yml.
    private static readonly Dictionary<string, string> Labels = new(StringComparer.Ordinal)
    {
        ["naval_retreat_speed"] = "Fleet speed while retreating",
        ["naval_retreat_speed_after_initial_combat"] = "Retreat speed after initial stage",
        ["sub_retreat_speed"] = "Submarine speed while retreating",
        ["convoy_retreat_speed"] = "Convoy speed while retreating",
        ["naval_hit_chance"] = "Naval Hit Chance",
        ["naval_light_gun_hit_chance_factor"] = "Light Gun hit chance",
        ["naval_heavy_gun_hit_chance_factor"] = "Heavy Gun hit chance",
        ["naval_torpedo_hit_chance_factor"] = "Torpedo hit chance"
    };

    /// <summary>The game's name of a modifier, or its key when it has none here.</summary>
    public static string Label(string key)
    {
        return Labels.GetValueOrDefault(key, key);
    }

    /// <summary>
    /// A one-line summary such as "Fleet speed while retreating +20%", formatted as the game shows it. Keys the engine
    /// does not read yet are marked "not simulated".
    /// </summary>
    public static string Summarize(IReadOnlyDictionary<string, double>? modifiers)
    {
        return string.Join(", ", (modifiers ?? new Dictionary<string, double>())
            .Where(entry => entry.Value != 0)
            .Select(entry => $"{Label(entry.Key)} {entry.Value * 100:+0.##;-0.##}%" + Status(entry.Key)));
    }

    // S-STATS-COUNTRY: whether combat reads the key, and if not, whether that is the game's own behavior.
    private static string Status(string key)
    {
        return BattleParticipant.IsModelledCountryModifier(key) ? string.Empty
            : BattleParticipant.CountryModifiersWithoutBattleEffect.Contains(key) ? " (no effect in battle)"
            : " (not simulated)";
    }

    /// <summary>A research's effects: its ship stat blocks, then its country modifiers.</summary>
    public static string SummarizeResearch(ResearchDto research)
    {
        var parts = new[]
        {
            StatInfo.Summarize(research.StatModifiers, research.StatAverages, research.StatMultipliers),
            Summarize(research.CountryModifiers)
        };
        return string.Join(", ", parts.Where(part => part.Length > 0));
    }
}
