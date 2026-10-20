namespace NavySimulator.GameData.Contracts;

/// <summary>
/// A-COVERAGE: a state's air base (<c>map/buildings.txt</c>, one <c>air_base</c> entry per state) with its map position,
/// written by <c>NavySimulator import-regions</c> to <c>air-bases/</c>. Land-based wings name their base by state ID.
/// </summary>
public class AirBaseDto
{
    /// <summary>The state's ID, e.g. 211 (Burgas).</summary>
    public int State { get; set; }

    /// <summary>The state's name (localisation of <c>STATE_&lt;id&gt;</c>).</summary>
    public string? Name { get; set; }

    /// <summary>The state's owner at the game's start (<c>history/states</c>), e.g. <c>BUL</c>.</summary>
    public string? Owner { get; set; }

    /// <summary>
    /// [RE] The point coverage is measured from: the centre of the box of the state's provinces (0xabce20), x from the left,
    /// y from the bottom, in pixels.
    /// </summary>
    public double X { get; set; }

    public double Y { get; set; }
}
