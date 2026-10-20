namespace NavySimulator.GameData.Contracts;

/// <summary>
/// S-TERRAIN: a naval terrain of <c>common/terrain</c> (<c>naval_terrain = yes</c>, e.g. <c>water_deep_ocean</c>), written by
/// <c>NavySimulator import-regions</c> to <c>naval-terrains/</c>. Keys are the game's.
/// </summary>
public class NavalTerrainDto
{
    public string ID { get; set; } = string.Empty;

    /// <summary>The game's name (localisation), e.g. "Deep Ocean".</summary>
    public string? Name { get; set; }

    /// <summary>The terrain's own modifiers (e.g. <c>positioning</c>, <c>navy_visibility</c>, <c>naval_mine_hit_chance</c>).</summary>
    public Dictionary<string, double> Modifiers { get; set; } = [];

    /// <summary>The numbers of each sub-unit block, by sub-unit (e.g. <c>submarine = { navy_visibility = 1.0 }</c>).</summary>
    public Dictionary<string, Dictionary<string, double>> SubUnitModifiers { get; set; } = [];

    /// <summary>The <c>units = { attack defence movement }</c> adjusters inside each sub-unit block, by sub-unit.</summary>
    public Dictionary<string, LeaderAdjusterDto> SubUnitAdjusters { get; set; } = [];
}
