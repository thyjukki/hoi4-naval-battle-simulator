namespace NavySimulator.Engine.Defines;

/// <summary>
/// Combat-relevant <c>NDefines.NMilitary</c> values. Property names match the game keys exactly so they can be bound
/// from <c>common/defines/*.lua</c>. Defaults are vanilla 1.19.3 (see docs/mechanics/defines.md for provenance).
/// </summary>
public sealed class NMilitaryDefines
{
    public double EXPERIENCE_COMBAT_FACTOR { get; set; } = 0.25;
}
