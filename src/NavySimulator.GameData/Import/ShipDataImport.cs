using NavySimulator.GameData.Contracts;

namespace NavySimulator.GameData.Import;

/// <summary>Hulls, modules, ship types and critical parts read from the game's files by <see cref="ShipDataImporter"/>.</summary>
public sealed class ShipDataImport
{
    public List<HullDto> Hulls { get; } = [];
    public List<ModuleDto> Modules { get; } = [];
    public List<ShipTypeDto> ShipTypes { get; } = [];
    public List<CriticalPartDto> CriticalParts { get; } = [];

    /// <summary>Script problems: unreadable files, duplicate definitions, slots that cannot be inherited.</summary>
    public List<string> Warnings { get; } = [];

    /// <summary>Equipment of a ship hull archetype that is not a Man the Guns hull (the non-DLC tech tree's ships).</summary>
    public List<string> SkippedEquipment { get; } = [];

    /// <summary>Numeric script keys with no stat in the simulator, with the hulls and modules that set them.</summary>
    public SortedDictionary<string, SortedSet<string>> IgnoredKeys { get; } = new(StringComparer.Ordinal);

    internal void Ignore(string key, string itemId)
    {
        if (!IgnoredKeys.TryGetValue(key, out var items))
        {
            items = new SortedSet<string>(StringComparer.Ordinal);
            IgnoredKeys[key] = items;
        }

        items.Add(itemId);
    }
}
