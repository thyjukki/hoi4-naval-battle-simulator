namespace NavySimulator.GameData.Contracts;

/// <summary>
/// A navy or air spirit: an idea of an idea category of <c>type = navy_spirit</c> or <c>air_spirit</c>
/// (<c>common/idea_tags</c>), written by <c>NavySimulator import-doctrines</c> to <c>spirits/</c> (S-SPIRIT).
/// </summary>
public class SpiritDto
{
    public string ID { get; set; } = string.Empty;

    /// <summary>The game's name (localisation), e.g. "Decisive Battle".</summary>
    public string? Name { get; set; }

    /// <summary>The idea category (the spirit slot), e.g. <c>naval_command_spirit</c>.</summary>
    public string Category { get; set; } = string.Empty;

    /// <summary>The category's name (localisation), e.g. "Spirit of Naval Command".</summary>
    public string? CategoryName { get; set; }

    /// <summary>
    /// The <c>has_doctrine</c> conditions of its <c>available</c> trigger: each group needs one of its doctrines (a grand
    /// doctrine or a slotted subdoctrine), and every group must be met.
    /// </summary>
    public List<List<string>> RequiredDoctrines { get; set; } = [];

    /// <summary>The other conditions of <c>available</c> and <c>visible</c> as script text; the simulator does not check them.</summary>
    public List<string> OtherConditions { get; set; } = [];

    public CountryEffectsDto Effects { get; set; } = new();
}
