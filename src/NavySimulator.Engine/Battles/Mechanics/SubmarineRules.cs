using NavySimulator.Engine.Defines;
using NavySimulator.Engine.Random;

namespace NavySimulator.Engine.Battles.Mechanics;

/// <summary>
/// S-SUB [RE]: submarines start hidden, can only be targeted while revealed, and are revealed by the enemy's sub
/// detection each hour or after firing torpedoes (0x1973521, 0x1973eea, 0x1975400). Spec: docs/mechanics/surface.md.
/// </summary>
public static class SubmarineRules
{
    /// <summary>
    /// [RE] Side detection value: mean sub detection over its ships in battle that have any, times
    /// NAVAL_COMBAT_SUB_DETECTION_FACTOR (the air term needs air, milestone 2).
    /// </summary>
    public static double Detection(IEnumerable<Ship> ships, GameDefines defines)
    {
        var detecting = ships
            .Where(ship => ship.IsInBattle && ship.IsVisible && ship.GetFinalStats().SubDetection > 0)
            .Select(ship => ship.GetFinalStats().SubDetection)
            .ToList();
        return detecting.Count == 0 ? 0 : detecting.Average() * defines.NNavy.NAVAL_COMBAT_SUB_DETECTION_FACTOR;
    }

    /// <summary>[RE] min(1, D * SUBMARINE_REVEAL_DETECTION_MULTIPLIER [* firing multiplier] / max(stealth - V, 0.1)).</summary>
    public static double RevealChance(Ship submarine, double enemyDetection, double ownPositioning, bool afterFiring, GameDefines defines)
    {
        var navy = defines.NNavy;
        var visibility = submarine.GetFinalStats().SubVisibility *
                         (1.0 + navy.SUBMARINE_REVEAL_ON_MINIMUM_POSITIONING * (1.0 - ownPositioning));
        var numerator = enemyDetection * navy.SUBMARINE_REVEAL_DETECTION_MULTIPLIER;

        if (afterFiring)
        {
            numerator *= navy.SUBMARINE_REVEAL_TORPEDO_FIRING_DETECTION_MULTIPLIER;
        }

        var divider = Math.Max(navy.SUBMARINE_BASE_STEALTH_VALUE - visibility, defines.Hardcoded.MinSubmarineStealthDivider);
        return Math.Min(1, numerator / divider);
    }

    /// <summary>[RE] Hourly step at the start of the submarine's update: count the reveal timer down, then roll if hidden.</summary>
    public static void HourlyReveal(Ship submarine, double enemyDetection, double ownPositioning, GameDefines defines, IRandomSource random)
    {
        if (submarine.RevealTimer > 0)
        {
            submarine.RevealTimer--;
        }

        if (submarine.RevealTimer == 0 && enemyDetection > 0 &&
            random.NextDouble(RollKind.SubmarineReveal) <= RevealChance(submarine, enemyDetection, ownPositioning, false, defines))
        {
            submarine.RevealTimer = (int)defines.NNavy.SUBMARINE_HIDE_TIMEOUT;
        }
    }

    /// <summary>[RE] After a submarine fires its torpedoes it may be revealed (resetting the timer if it already is).</summary>
    public static void RevealAfterFiring(Ship submarine, double enemyDetection, double ownPositioning, GameDefines defines, IRandomSource random)
    {
        if (enemyDetection > 0 && random.NextDouble(RollKind.SubmarineReveal) <= RevealChance(submarine, enemyDetection, ownPositioning, true, defines))
        {
            submarine.RevealTimer = (int)defines.NNavy.SUBMARINE_HIDE_TIMEOUT;
        }
    }
}
