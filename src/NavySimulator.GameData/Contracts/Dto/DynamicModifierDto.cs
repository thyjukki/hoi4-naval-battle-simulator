namespace NavySimulator.GameData.Contracts;

/// <summary>S-NATIONAL-DM: a dynamic modifier (<c>common/dynamic_modifiers</c>) a country's history or focuses add.</summary>
public class DynamicModifierDto
{
    public string ID { get; set; } = string.Empty;

    /// <summary>The game's name (localisation).</summary>
    public string? Name { get; set; }

    /// <summary>Modifiers with a number as value.</summary>
    public Dictionary<string, double> Modifiers { get; set; } = [];

    /// <summary>Modifiers whose value is a country variable: modifier key → variable name.</summary>
    public Dictionary<string, string> VariableModifiers { get; set; } = [];
}
