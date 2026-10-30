using System.Globalization;
using NavySimulator.Engine.Battles;
using NavySimulator.Engine.Nations;
using NavySimulator.GameData.Contracts;

namespace NavySimulator.Web.Services;

/// <summary>One variable a dynamic modifier reads (S-NATIONAL-DM): its value after history, focuses and the hand-set value.</summary>
public sealed record NationalVariable(string Name, IReadOnlyList<string> Modifiers, double Value, bool SetByHand);

/// <summary>Display and editing helpers for a side's national focuses, spirits and dynamic modifiers (S-NATIONAL).</summary>
public static class NationalInfo
{
    /// <summary>
    /// What an effect statement does, e.g. "adds Supremacy of the Battlefleet" or "Torpedo hit chance (variable) +0.05";
    /// an effect inside an <c>if</c> is marked "conditional" (the simulator takes the branch).
    /// </summary>
    public static string Describe(NationalEffectDto effect, NationDto nation)
    {
        var text = effect.Effect switch
        {
            "add_ideas" => $"adds {SpiritName(nation, effect.Target)}" + (effect.Days is { } days ? $" for {days} days" : string.Empty),
            "remove_ideas" => $"removes {SpiritName(nation, effect.Target)}",
            "add_dynamic_modifier" => $"adds {ModifierName(nation, effect.Target)}",
            "remove_dynamic_modifier" => $"removes {ModifierName(nation, effect.Target)}",
            _ => $"{VariableLabel(nation, effect.Target)} {Operation(effect)}"
        };
        return effect.Condition is null ? text : $"{text} (conditional)";
    }

    /// <summary>A focus's effects in one line; adding a dynamic modifier the side already has is left out.</summary>
    public static string Summarize(NationalFocusDto focus, NationDto nation)
    {
        var history = nation.History.Where(effect => effect.Effect == "add_dynamic_modifier").Select(effect => effect.Target).ToHashSet(StringComparer.Ordinal);
        return string.Join(", ", focus.Effects
            .Where(effect => effect.Effect != "add_dynamic_modifier" || !history.Contains(effect.Target))
            .Select(effect => Describe(effect, nation)));
    }

    /// <summary>A national spirit's effects, its targeted modifiers included.</summary>
    public static string Summarize(NationalSpiritDto spirit)
    {
        var targeted = spirit.TargetedModifiers.SelectMany(block => block.Modifiers.Select(entry =>
            string.Create(CultureInfo.InvariantCulture, $"{entry.Key} {entry.Value * 100:+0.##;-0.##}% against {block.Tag} (no effect at sea)")));
        return string.Join(", ", new[] { DoctrineInfo.Summarize(spirit.Effects) }.Concat(targeted).Where(part => part.Length > 0));
    }

    /// <summary>How the country gets a national spirit, e.g. "1936 history", "focus Heavier Naval Guns", "against JAP".</summary>
    public static string Sources(NationalSpiritDto spirit, NationDto nation)
    {
        return string.Join(", ", spirit.Sources.Select(source => source switch
        {
            "history" => "1936 history",
            "allowed" or "id" => "events or decisions",
            _ when source.StartsWith("focus:", StringComparison.Ordinal) => $"focus {FocusName(nation, source["focus:".Length..])}",
            _ when source.StartsWith("against:", StringComparison.Ordinal) => $"targets {source["against:".Length..]}",
            _ => source.Replace("focus:", "focus ", StringComparison.Ordinal)
        }).Distinct());
    }

    /// <summary>The side's resolved national state (history, focuses, hand edits), or the empty state without a setup.</summary>
    public static NationalState State(NationalSetup? setup)
    {
        return NationalRules.Resolve(setup);
    }

    /// <summary>
    /// Turns a national spirit on or off by hand: undoes the opposite hand edit, else records this one. Hand edits that
    /// only restore what history and focuses give are dropped.
    /// </summary>
    public static void SetSpirit(NationalSetupDto national, string spiritId, bool active, bool activeWithoutHandEdits)
    {
        national.AddedSpirits.Remove(spiritId);
        national.RemovedSpirits.Remove(spiritId);

        if (active != activeWithoutHandEdits)
        {
            (active ? national.AddedSpirits : national.RemovedSpirits).Add(spiritId);
        }
    }

    /// <summary>
    /// The variables the nation's dynamic modifiers read for modelled modifiers, with their current values; only those of
    /// active dynamic modifiers.
    /// </summary>
    public static IReadOnlyList<NationalVariable> Variables(NationDto nation, NationalSetupDto national, NationalState state)
    {
        return nation.DynamicModifiers.Where(modifier => state.DynamicModifiers.Contains(modifier.ID))
            .SelectMany(modifier => modifier.VariableModifiers.Where(entry => BattleParticipant.IsModelledCountryModifier(entry.Key)))
            .GroupBy(entry => entry.Value, StringComparer.Ordinal)
            .Select(group => new NationalVariable(group.Key, group.Select(entry => entry.Key).Distinct(StringComparer.Ordinal).ToList(),
                state.Variables.GetValueOrDefault(group.Key), national.Variables.ContainsKey(group.Key)))
            .ToList();
    }

    /// <summary>The section summary, e.g. "JAP: 1936 history, 2 focuses, 1 national spirit, 2 dynamic modifiers".</summary>
    public static string Summary(BattleParticipantDto side, NationalState state)
    {
        if (side.National is not { } national)
        {
            return "none";
        }

        var parts = new List<string> { national.FromHistory ? "1936 history" : "no history" };
        parts.Add(Count(national.Focuses.Count, "focus", "focuses"));
        parts.Add(Count(state.Spirits.Count, "national spirit", "national spirits"));
        parts.Add(Count(state.DynamicModifiers.Count, "dynamic modifier", "dynamic modifiers"));
        return $"{side.Country}: {string.Join(", ", parts)}";
    }

    private static string Count(int count, string one, string many)
    {
        return $"{count} {(count == 1 ? one : many)}";
    }

    private static string Operation(NationalEffectDto effect)
    {
        var value = effect.Value ?? 0;
        return effect.Effect switch
        {
            "set_variable" => $"= {Number(value)}",
            "add_to_variable" => $"{(value < 0 ? "-" : "+")}{Number(Math.Abs(value))}",
            "subtract_from_variable" => $"{(value < 0 ? "+" : "-")}{Number(Math.Abs(value))}",
            "multiply_variable" => $"× {Number(value)}",
            _ => $"{effect.Effect} {Number(value)}"
        };
    }

    // A variable by the modifiers it feeds, e.g. "Torpedo hit chance (JAP_navy_naval_torpedo_hit_chance_factor)".
    private static string VariableLabel(NationDto nation, string variable)
    {
        var keys = nation.DynamicModifiers.SelectMany(modifier => modifier.VariableModifiers.Where(entry => entry.Value == variable).Select(entry => entry.Key))
            .Distinct(StringComparer.Ordinal)
            .Select(ModifierInfo.Label)
            .ToList();
        return keys.Count == 0 ? variable : $"{string.Join(", ", keys)} ({variable})";
    }

    private static string SpiritName(NationDto nation, string id)
    {
        return nation.Spirits.FirstOrDefault(spirit => spirit.ID == id)?.Name ?? id;
    }

    private static string ModifierName(NationDto nation, string id)
    {
        return nation.DynamicModifiers.FirstOrDefault(modifier => modifier.ID == id)?.Name ?? id;
    }

    private static string FocusName(NationDto nation, string id)
    {
        return nation.Focuses.FirstOrDefault(focus => focus.ID == id)?.Name ?? id;
    }

    private static string Number(double value)
    {
        return value.ToString("0.###", CultureInfo.InvariantCulture);
    }
}
