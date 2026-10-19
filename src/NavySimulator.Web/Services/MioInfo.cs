using NavySimulator.GameData.Contracts;
using NavySimulator.GameData.Import;

namespace NavySimulator.Web.Services;

/// <summary>Display and editing helpers for MIOs and a side's MIO setups (S-MIO, S-MIO-TREE).</summary>
public static class MioInfo
{
    // S-STATS-MIO [RE]: these three are added to the design's value; every other stat joins its multiplier.
    private static readonly HashSet<string> AddedStats =
        new(["naval_light_gun_hit_chance_factor", "naval_heavy_gun_hit_chance_factor", "naval_torpedo_hit_chance_factor"], StringComparer.Ordinal);

    /// <summary>"Brooklyn Naval Yard (USA)": the game's name and the countries that can have it.</summary>
    public static string Label(MioDto mio)
    {
        var countries = (mio.Countries ?? []).Count == 0 ? string.Empty : $" ({string.Join(", ", mio.Countries!)})";
        return $"{mio.Name ?? mio.ID}{countries}";
    }

    /// <summary>
    /// The option group of an MIO in pickers: the first country of its <c>allowed</c> trigger, "Any country" (the generic
    /// ones), and last, in parentheses, archetypes and debug-mode MIOs.
    /// </summary>
    public static string Group(MioDto mio)
    {
        return mio.ArchetypeOnly ? "Archetypes (no country has them)"
            : mio.DebugOnly ? "Debug mode only (is_debug)"
            : (mio.Countries ?? []).Count == 0 ? "Any country" : mio.Countries![0];
    }

    /// <summary>
    /// A one-line summary of an equipment bonus, e.g. "Armor +15%, Light hit chance +0.05". Hit chance factors are added
    /// to the design (S-STATS-MIO); keys the simulator has no stat for are marked "not simulated".
    /// </summary>
    public static string Summarize(Dictionary<string, double>? bonus)
    {
        return string.Join(", ", (bonus ?? []).Select(entry => Stat(entry.Key, entry.Value)));
    }

    /// <summary>What a trait gives: its equipment bonus, then production and organization bonuses (no effect in battle).</summary>
    public static string Summarize(MioTraitDto trait)
    {
        var parts = new List<string>();
        var equipment = Summarize(trait.EquipmentBonus);

        if (equipment.Length > 0)
        {
            parts.Add(equipment);
        }

        parts.AddRange((trait.ProductionBonus ?? []).Concat(trait.OrganizationModifiers ?? [])
            .Select(entry => $"{entry.Key.Replace("military_industrial_organization_", string.Empty).Replace('_', ' ')} {(Math.Abs(entry.Value) < 1 ? Percent(entry.Value) : Flat(entry.Value))} (no effect in battle)"));
        return string.Join(", ", parts);
    }

    /// <summary>A policy's equipment bonus blocks, e.g. "all designs: Range +25%, ship_hull_carrier: Carrier size +1".</summary>
    public static string Summarize(MioPolicyDto policy)
    {
        var parts = (policy.EquipmentBonus ?? [])
            .Select(block => $"{(block.Key == MioImporter.SameAsMio ? "all designs" : block.Key)}: {Summarize(block.Value)}")
            .ToList();
        return parts.Count == 0 ? "no effect in battle" : string.Join("; ", parts);
    }

    /// <summary>
    /// S-MIO-TREE [RE]: each trait of the tree with whether the setup unlocks it, whether its parent groups hold, the
    /// unlocked traits its own <c>mutually_exclusive</c> list names (the game's unlock check), and those of them that
    /// list it back (a pair the game never allows together).
    /// </summary>
    public static IReadOnlyList<MioTraitState> Traits(MioDto mio, MioSetupDto? setup)
    {
        var unlocked = (setup?.Traits ?? []).ToHashSet(StringComparer.Ordinal);
        var traits = mio.Traits ?? [];
        return traits.Select(trait =>
            {
                var parentsMet = (trait.Parents ?? []).All(parent => (parent.Traits ?? []).Count(unlocked.Contains) >= parent.Needed);
                var excludedBy = traits
                    .Where(other => other.ID != trait.ID && unlocked.Contains(other.ID) && (trait.MutuallyExclusive ?? []).Contains(other.ID))
                    .ToList();
                var conflicts = excludedBy.Where(other => (other.MutuallyExclusive ?? []).Contains(trait.ID)).Select(other => other.ID).ToList();
                return new MioTraitState(trait, unlocked.Contains(trait.ID), parentsMet, excludedBy.Select(other => other.ID).ToList(), conflicts);
            })
            .ToList();
    }

    /// <summary>Unlocks or locks a trait of the side's setup of <paramref name="mioId"/>; an empty setup is removed.</summary>
    public static void SetTrait(BattleParticipantDto side, string mioId, string traitId, bool unlocked)
    {
        var setup = side.Mios.GetValueOrDefault(mioId) ?? new MioSetupDto();
        setup.Traits.Remove(traitId);

        if (unlocked)
        {
            setup.Traits.Add(traitId);
        }

        Store(side, mioId, setup);
    }

    /// <summary>Attaches a policy (null: none) to the side's setup of <paramref name="mioId"/>; an empty setup is removed.</summary>
    public static void SetPolicy(BattleParticipantDto side, string mioId, string? policyId)
    {
        var setup = side.Mios.GetValueOrDefault(mioId) ?? new MioSetupDto();
        setup.Policy = string.IsNullOrEmpty(policyId) ? null : policyId;
        Store(side, mioId, setup);
    }

    private static void Store(BattleParticipantDto side, string mioId, MioSetupDto setup)
    {
        if (setup.Traits.Count == 0 && setup.Policy is null)
        {
            side.Mios.Remove(mioId);
        }
        else
        {
            side.Mios[mioId] = setup;
        }
    }

    private static string Stat(string key, double value)
    {
        var stat = ShipStatKeys.ByGameKey.TryGetValue(key, out var property)
            ? StatInfo.All.FirstOrDefault(info => info.Key.Equals(property, StringComparison.OrdinalIgnoreCase))
            : null;

        if (stat is null)
        {
            return $"{key} {Percent(value)} (not simulated)";
        }

        // Carrier size's multiplier is a flat add (S-STATS-DESIGN).
        return AddedStats.Contains(key) || key == "carrier_size" ? $"{stat.Label} {Flat(value)}" : $"{stat.Label} {Percent(value)}";
    }

    private static string Flat(double value)
    {
        return value.ToString("+0.###;-0.###", System.Globalization.CultureInfo.InvariantCulture);
    }

    private static string Percent(double value)
    {
        return (value * 100).ToString("+0.##;-0.##", System.Globalization.CultureInfo.InvariantCulture) + "%";
    }
}
