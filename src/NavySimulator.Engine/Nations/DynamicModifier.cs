using NavySimulator.Engine.Doctrines;
using NavySimulator.Engine.Stats;

namespace NavySimulator.Engine.Nations;

/// <summary>
/// S-NATIONAL-DM [PDX]: a dynamic modifier (<c>common/dynamic_modifiers</c>): country modifiers with fixed values or
/// values read from the country's variables (e.g. TFB's <c>JAP_imperial_navy_dm</c>:
/// <c>naval_torpedo_hit_chance_factor = JAP_navy_naval_torpedo_hit_chance_factor</c>).
/// </summary>
public sealed class DynamicModifier(string id, ModifierSet modifiers, IReadOnlyDictionary<string, string> variableModifiers)
{
    public string ID { get; } = id;

    /// <summary>The modifiers with a number as value.</summary>
    public ModifierSet Modifiers { get; } = modifiers;

    /// <summary>The modifiers whose value is a country variable: modifier key → variable name.</summary>
    public IReadOnlyDictionary<string, string> VariableModifiers { get; } = variableModifiers;

    /// <summary>S-NATIONAL-DM [PDX]: the modifiers with the variables' current values; an unset variable is 0.</summary>
    public CountryEffects Effects(IReadOnlyDictionary<string, double> variables)
    {
        var resolved = VariableModifiers.Select(entry => new KeyValuePair<string, double>(entry.Key, variables.GetValueOrDefault(entry.Value)));
        return new CountryEffects(Modifiers.Add(new ModifierSet(resolved)), [], []);
    }
}
