using System.Text.Json.Nodes;

namespace NavySimulator.GameData.Contracts;

/// <summary>
/// An override set of <c>overrides/</c>: changes to defines, static modifiers and imported content, applied over the
/// define sources and the data directory when a scenario is built, so a mod change can be tried without editing the
/// game's or the mod's files (docs/overrides.md).
/// </summary>
public class OverrideSetDto
{
    public string ID { get; set; } = string.Empty;

    public string? Name { get; set; }

    public string? Description { get; set; }

    /// <summary>
    /// Define values by <c>Category.KEY</c> (e.g. <c>NNavy.COMBAT_BASE_HIT_CHANCE</c>): a number, a boolean, a string or
    /// an array of numbers, as in a defines file.
    /// </summary>
    public Dictionary<string, JsonNode?> Defines { get; set; } = [];

    /// <summary>
    /// Static modifier values by block, then modifier (e.g. <c>screening_bonus → naval_hit_chance</c>). A key is set in
    /// the block the define sources gave; null removes it.
    /// </summary>
    public Dictionary<string, Dictionary<string, double?>> StaticModifiers { get; set; } = [];

    /// <summary>Changes to imported content (hulls, modules, doctrines, spirits, MIOs, traits, ...), applied in order.</summary>
    public List<ContentOverrideDto> Content { get; set; } = [];
}
