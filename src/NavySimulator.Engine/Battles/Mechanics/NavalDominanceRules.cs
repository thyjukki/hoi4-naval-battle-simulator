using NavySimulator.Engine.Defines;
using NavySimulator.Engine.Stats;

namespace NavySimulator.Engine.Battles.Mechanics;

/// <summary>
/// S-DOMINANCE [RE] (0xbd5d60, 0xfafda0, 0x1003c50, 0x1003f50). Spec: docs/mechanics/surface.md. The game keeps naval
/// dominance per strategic region from every task force on a mission there; the battle only has its two fleets, so each
/// side's target is the sum over its ships, as if both patrolled the region [ASSUMED], and its current value follows the
/// target once a day (S-DOMINANCE-DAILY).
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
    /// S-DOMINANCE-DAILY [RE + LOG] 0x1003c50, once a day at the day change: with a target, the current value gains
    /// <c>trunc(target × DOMINANCE_DAILY_GAIN_FACTOR)</c>, capped at the target (so sunk ships take it straight down to the
    /// new target), and the target becomes the value a later decline starts from; without one (no task force left), it
    /// loses <c>trunc(declineFrom × DOMINANCE_DAILY_LOSS_FACTOR)</c>, down to 0. Fixed point, products truncated.
    /// </summary>
    public static (double Current, double DeclineFrom) DailyUpdate(double current, double target, double declineFrom, GameDefines defines)
    {
        var navy = defines.NNavy;

        if (target > 0)
        {
            return (Math.Clamp(current + FixedPoint.Truncate(target * navy.DOMINANCE_DAILY_GAIN_FACTOR), 0, target), target);
        }

        return (Math.Max(0, current - FixedPoint.Truncate(declineFrom * navy.DOMINANCE_DAILY_LOSS_FACTOR)), declineFrom);
    }

    /// <summary>
    /// S-DOMINANCE [RE] 0x1003f50: a side controls the region when its current dominance is at least the enemy's (on a
    /// tie both do) and at least <c>t / (1 − t)</c> times the region's target value, t =
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
