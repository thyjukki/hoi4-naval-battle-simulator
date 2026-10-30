namespace NavySimulator.Engine.Defines;

/// <summary>
/// <c>NDefines.NGraphics</c> values the province darkness reads (S-NIGHT, <c>common/defines/00_graphics.lua</c>). Property
/// names match the game keys exactly so they can be bound from <c>common/defines/*.lua</c>. Defaults are vanilla 1.19.4 (the same as 1.19.3).
/// </summary>
public sealed class NGraphicsDefines
{
    /// <summary>X position on map, of Greenwitch GMT+0 (an integer in the game).</summary>
    public double GMT_OFFSET { get; set; } = 2793;

    /// <summary>Feather value between complete darkness and the day.</summary>
    public double DAY_NIGHT_FEATHER { get; set; } = 0.024;

    /// <summary>Our map is missing big parts of globe on north and south.</summary>
    public double SOUTH_POLE_OFFSET { get; set; } = 0.17;

    public double NORTH_POLE_OFFSET { get; set; } = 0.93;
}
