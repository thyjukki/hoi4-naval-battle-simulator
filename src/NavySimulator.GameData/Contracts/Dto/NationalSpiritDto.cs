namespace NavySimulator.GameData.Contracts;

/// <summary>S-NATIONAL-SPIRIT: a national spirit (an idea outside the spirit slots) a country can have.</summary>
public class NationalSpiritDto
{
    public string ID { get; set; } = string.Empty;

    /// <summary>The game's name (localisation of the idea's <c>name</c>, else its ID).</summary>
    public string? Name { get; set; }

    /// <summary>The idea category, e.g. <c>country</c> or <c>hidden_ideas</c>.</summary>
    public string Category { get; set; } = string.Empty;

    /// <summary>
    /// How the country gets it: <c>history</c>, <c>focus:&lt;focus&gt;</c>, <c>&lt;TAG&gt; focus:&lt;focus&gt;</c> (another
    /// country's focus gives it), or <c>allowed</c> (its <c>allowed</c> trigger or ID names the country).
    /// </summary>
    public List<string> Sources { get; set; } = [];

    public CountryEffectsDto Effects { get; set; } = new();

    /// <summary>Its <c>targeted_modifier</c> blocks: modifiers against one country.</summary>
    public List<TargetedModifierDto> TargetedModifiers { get; set; } = [];
}
