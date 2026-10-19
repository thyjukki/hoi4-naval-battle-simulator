namespace NavySimulator.Engine.Defines;

/// <summary>
/// Combat-relevant <c>NDefines.NMilitary</c> values. Property names match the game keys exactly so they can be bound
/// from <c>common/defines/*.lua</c>. Defaults are vanilla 1.19.3 (see docs/mechanics/defines.md for provenance).
/// </summary>
public sealed class NMilitaryDefines
{
    public double EXPERIENCE_COMBAT_FACTOR { get; set; } = 0.25;

    /// <summary>Shortest time possible for a combat in hours</summary>
    public double COMBAT_MINIMUM_TIME { get; set; } = 4.0;

    /// <summary>Experience level without bonus or malus (ships use the same levels as land units).</summary>
    public double ARMY_EXP_BASE_LEVEL { get; set; } = 1.0;

    /// <summary>Experience needed to progress to the next level</summary>
    public double[] UNIT_EXP_LEVELS { get; set; } = [0.1, 0.3, 0.75, 0.9];

    /// <summary>Base org regain per hour</summary>
    public double SHIP_MORALE_TO_ORG_REGAIN_BASE { get; set; } = 0.2;
}
