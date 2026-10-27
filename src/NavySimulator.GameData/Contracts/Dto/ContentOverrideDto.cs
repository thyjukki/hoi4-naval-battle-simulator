using System.Text.Json.Nodes;

namespace NavySimulator.GameData.Contracts;

/// <summary>
/// One change of an override set to an imported item: the value at <see cref="Path"/> inside item
/// <see cref="ID"/> of <see cref="Collection"/> (a data folder, e.g. <c>spirits</c>).
/// </summary>
public class ContentOverrideDto
{
    /// <summary>The data folder of the item, e.g. <c>modules</c>, <c>doctrines</c>, <c>leader-traits</c>.</summary>
    public string Collection { get; set; } = string.Empty;

    /// <summary>The item's ID (for <c>doctrines</c> the folder, e.g. <c>naval</c>).</summary>
    public string ID { get; set; } = string.Empty;

    /// <summary>
    /// The field inside the item, as JSON property names separated by dots, with <c>[id]</c> picking a list entry by
    /// its ID: <c>statModifiers.lightAttack</c>, <c>subdoctrines[x].rewards[y].effects.countryModifiers.naval_hit_chance</c>.
    /// </summary>
    public string Path { get; set; } = string.Empty;

    /// <summary>The new value; null removes a dictionary key (or clears the field).</summary>
    public JsonNode? Value { get; set; }
}
