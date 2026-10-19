using NavySimulator.Engine.Defines;
using NavySimulator.Engine.Doctrines;
using NavySimulator.GameData.Contracts;
using NavySimulator.GameData.Import;

namespace NavySimulator.Web.Services;

/// <summary>Display helpers for doctrines and spirits (S-DOCTRINE, S-SPIRIT).</summary>
public static class DoctrineInfo
{
    /// <summary>
    /// A one-line summary of a doctrine part's or spirit's effects, e.g. "Capital ship defense +5%, battleship: Organization
    /// +7.5". Parts combat ignores are marked "not simulated".
    /// </summary>
    public static string Summarize(CountryEffectsDto? effects)
    {
        if (effects is null)
        {
            return string.Empty;
        }

        var parts = new List<string>();
        var country = ModifierInfo.Summarize(effects.CountryModifiers);

        if (country.Length > 0)
        {
            parts.Add(country);
        }

        parts.AddRange((effects.UnitBonuses ?? []).Select(block => $"{block.Key}: {Stats(block.Value)}"));
        parts.AddRange((effects.EquipmentBonuses ?? []).Select(block => $"{block.Key} equipment: {Stats(block.Value)}"));
        parts.AddRange((effects.Scripted ?? []).Select(scripted => $"{scripted} (not simulated)"));
        return string.Join(", ", parts);
    }

    /// <summary>
    /// The rewards of a subdoctrine with the mastery each needs and whether <paramref name="mastery"/> unlocks it
    /// (S-DOCTRINE-MASTERY). Uses the vanilla <c>DEFAULT_REWARD_MASTERY</c> unless <paramref name="defines"/> is given.
    /// </summary>
    public static IReadOnlyList<(DoctrineRewardDto Reward, double Threshold, bool Unlocked)> Rewards(
        SubdoctrineDto subdoctrine,
        double mastery,
        GameDefines? defines = null)
    {
        var defaultMastery = (defines ?? GameDefines.Vanilla()).NDoctrines.DEFAULT_REWARD_MASTERY;
        var threshold = 0.0;
        var rewards = new List<(DoctrineRewardDto, double, bool)>();

        foreach (var reward in subdoctrine.Rewards ?? [])
        {
            threshold += reward.Mastery is { } cost && cost != 0 ? cost : defaultMastery;
            rewards.Add((reward, threshold, mastery >= threshold));
        }

        return rewards;
    }

    /// <summary>"needs Fleet in Being or Line of Battle", or null when the side meets the spirit's doctrine conditions.</summary>
    public static string? MissingDoctrines(SpiritDto spirit, IReadOnlyList<DoctrineSetup> doctrines, Func<string, string> name)
    {
        var missing = (spirit.RequiredDoctrines ?? [])
            .Where(group => !group.Any(id => DoctrineRules.HasDoctrine(doctrines, id)))
            .Select(group => string.Join(" or ", group.Select(name)))
            .ToList();
        return missing.Count == 0 ? null : $"needs {string.Join(" and ", missing)}";
    }

    private static string Stats(Dictionary<string, double>? stats)
    {
        return string.Join(", ", (stats ?? []).Select(entry => Stat(entry.Key, entry.Value)));
    }

    // Sub-unit and equipment bonuses scale the stat (S-STATS-SHIP, S-STATS-BONUS); organization is added flat.
    private static string Stat(string key, double value)
    {
        if (key == "max_organisation")
        {
            return $"Organization {value:+0.##;-0.##}";
        }

        var stat = ShipStatKeys.ByGameKey.TryGetValue(key, out var property)
            ? StatInfo.All.FirstOrDefault(info => info.Key.Equals(property, StringComparison.OrdinalIgnoreCase))
            : null;
        return stat is null
            ? $"{key} {value * 100:+0.##;-0.##}% (not simulated)"
            : $"{stat.Label} {value * 100:+0.##;-0.##}%";
    }
}
