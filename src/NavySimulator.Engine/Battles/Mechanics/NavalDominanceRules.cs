using NavySimulator.Engine.Defines;

namespace NavySimulator.Engine.Battles.Mechanics;

/// <summary>
/// S-DOMINANCE [RE] (0xbd5d60, 0xfafda0, 0x1003f50). Spec: docs/mechanics/surface.md. The game keeps naval dominance
/// per strategic region from every task force on a mission there; the battle only has its two fleets, so each side's
/// dominance is the sum over its ships at the start of the battle, as if both patrolled the region [ASSUMED].
/// </summary>
public static class NavalDominanceRules
{
    /// <summary>
    /// S-DOMINANCE [RE] 0xbd5d60: a ship's dominance, from its variant stats:
    /// <c>naval_dominance_factor × (1 + (speed − S) / 2S + (range − R) / 2R + heavy attack × H + carrier size × C)</c>,
    /// with S, R, H and C the <c>DOMINANCE_PER_SHIP_PER_*</c> defines; a term with a zero neutral value is 0.
    /// </summary>
    public static double ShipDominance(Ship ship, GameDefines defines)
    {
        var navy = defines.NNavy;
        var stats = ship.VariantStats;
        var factor = 1 + Relative(stats.Speed, navy.DOMINANCE_PER_SHIP_PER_SPEED_NEUTRAL) +
                     Relative(stats.NavalRange, navy.DOMINANCE_PER_SHIP_PER_RANGE_NEUTRAL) +
                     stats.HeavyAttack * navy.DOMINANCE_PER_SHIP_PER_HEAVY_GUN_ATTACK +
                     stats.CarrierSize * navy.DOMINANCE_PER_SHIP_PER_CARRIER_SIZE;

        return ship.Design.Hull.NavalDominanceFactor * factor;
    }

    /// <summary>
    /// S-DOMINANCE [RE] 0xfafda0: a task force's dominance in a mission region is the sum over its ships times the
    /// mission's efficiency and <c>MISSION_DOMINANCE_RATIOS</c>. Both sides are taken to patrol the battle's region
    /// with the same efficiency [ASSUMED], so the sum alone decides.
    /// </summary>
    public static double SideDominance(IEnumerable<Ship> ships, GameDefines defines)
    {
        return ships.Where(ship => !ship.IsSunk).Sum(ship => ShipDominance(ship, defines));
    }

    /// <summary>
    /// S-DOMINANCE [RE] 0x1003f50: a side controls the region when its dominance is at least the enemy's (on a tie
    /// both do) and at least <c>t / (1 − t)</c> times the region's target value, t =
    /// <c>DOMINANCE_CONTROLLED_THRESHOLD_RATIO</c>. The region's target value is not modelled: any dominance reaches it
    /// [ASSUMED].
    /// </summary>
    public static bool IsDominant(double own, double enemy)
    {
        return own > 0 && own >= enemy;
    }

    private static double Relative(double value, double neutral)
    {
        return neutral == 0 ? 0 : (value - neutral) / (2 * neutral);
    }
}
