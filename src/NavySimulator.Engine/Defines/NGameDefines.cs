namespace NavySimulator.Engine.Defines;

/// <summary>
/// <c>NDefines.NGame</c> values the simulator reads (A-COVERAGE). Property names match the game keys exactly so they can be
/// bound from <c>common/defines/*.lua</c>. Defaults are vanilla 1.19.4 (the same as 1.19.3).
/// </summary>
public sealed class NGameDefines
{
    /// <summary>Yes, we did the math</summary>
    public double MAP_SCALE_PIXEL_TO_KM { get; set; } = 7.114;
}
