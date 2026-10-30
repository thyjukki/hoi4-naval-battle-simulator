namespace NavySimulator.GameData.Contracts;

/// <summary>An idea's <c>targeted_modifier</c> block: modifiers (mostly <c>_against</c> keys) against the country <see cref="Tag"/>.</summary>
public class TargetedModifierDto
{
    public string Tag { get; set; } = string.Empty;
    public Dictionary<string, double> Modifiers { get; set; } = [];
}
