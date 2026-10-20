namespace NavySimulator.Engine;

/// <summary>
/// A-EXTERNAL: land-based planes of one plane type from one air base, on a mission over the battle's region.
/// <paramref name="MissionEfficiency"/> is the mission's efficiency (an input), which scales the planes offered to the join
/// cap; with an <paramref name="AirBase"/> (state ID) only the part the simulator does not compute (A-COVERAGE: coverage,
/// crowding and supply come from the base); <paramref name="DisruptionFactor"/> multiplies their attack (1 unless enemy
/// interceptors disrupt the mission; an input). <paramref name="Mission"/> is the game's mission keyword: only
/// <see cref="NavalStrike"/> wings join a naval battle (A-WINGS-EXT); air superiority wings fight in the region's air war.
/// </summary>
public record LandBasedAirwing(
    string PlaneID,
    int Planes,
    double MissionEfficiency = 1.0,
    double DisruptionFactor = 1.0,
    string Mission = LandBasedAirwing.NavalStrike,
    int? AirBase = null)
{
    /// <summary>The naval strike mission (game keyword <c>naval_bomber</c>, mission bit 0x10).</summary>
    public const string NavalStrike = "naval_bomber";

    /// <summary>The air superiority mission (mission bit 0x1).</summary>
    public const string AirSuperiority = "air_superiority";

    public bool JoinsNavalBattle => Mission == NavalStrike;
}
