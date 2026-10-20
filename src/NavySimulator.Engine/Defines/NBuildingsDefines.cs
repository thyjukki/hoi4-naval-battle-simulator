namespace NavySimulator.Engine.Defines;

/// <summary>
/// <c>NDefines.NBuildings</c> values the simulator reads (A-COVERAGE). Property names match the game keys exactly so they
/// can be bound from <c>common/defines/*.lua</c>. Defaults are vanilla 1.19.3.
/// </summary>
public sealed class NBuildingsDefines
{
    /// <summary>Each level of airbase building multiplied by this, gives capacity (max operational value). Value is int. 1 for each airplane.</summary>
    public double AIRBASE_CAPACITY_MULT { get; set; } = 200;
}
