namespace NavySimulator.Engine;

/// <summary>
/// S-DESIGN-SLOT [PDX]: one module slot of a hull (<c>module_slots</c>). Slots whose name starts with <c>fixed_</c> are
/// grouped as fixed slots in the game's designer, the others are custom slots. <see cref="GameOrder"/> is the game's
/// iteration order of design slots, which critical parts are picked in (S-CRIT-PART).
/// </summary>
public sealed record ModuleSlot(string ID, bool Required, IReadOnlyList<string> AllowedCategories, int GameOrder = 0)
{
    public bool IsFixed => ID.StartsWith("fixed_", StringComparison.Ordinal);

    public bool Allows(StatModule module)
    {
        return AllowedCategories.Contains(module.Category, StringComparer.Ordinal);
    }
}
