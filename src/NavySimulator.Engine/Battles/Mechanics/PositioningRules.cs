using NavySimulator.Engine.Defines;

namespace NavySimulator.Engine.Battles.Mechanics;

/// <summary>
/// S-POS: positioning (0..1) and its effects. Spec: docs/mechanics/surface.md.
/// </summary>
public static class PositioningRules
{
    public static double Calculate(BattleLines own, BattleLines enemy, GameDefines defines)
    {
        var navy = defines.NNavy;
        var positioning = navy.BASE_POSITIONING;
        positioning -= FleetSizePenalty(own.Count, enemy.Count, defines);
        positioning += DetectionBonus(own, enemy, defines);
        return Math.Clamp(positioning, 0, 1);
    }

    // S-POS-SIZE [DEFINE + ASSUMED shape]: penalty for each +100% ship ratio above the enemy, capped.
    public static double FleetSizePenalty(int ownShips, int enemyShips, GameDefines defines)
    {
        var navy = defines.NNavy;

        if (enemyShips <= 0 || ownShips < navy.MIN_SHIPS_FOR_HIGHER_SHIP_RATIO_PENALTY || ownShips <= enemyShips)
        {
            return 0;
        }

        var ratioAboveParity = ownShips / (double)enemyShips - 1.0;
        return Math.Min(navy.MAX_POSITIONING_PENALTY_FROM_HIGHER_SHIP_RATIO, ratioAboveParity * navy.HIGHER_SHIP_RATIO_POSITIONING_PENALTY_FACTOR);
    }

    // S-POS-DETECT [DEFINE + ASSUMED aggregation]: bonus from out-detecting the enemy, using each side's best surface detection.
    public static double DetectionBonus(BattleLines own, BattleLines enemy, GameDefines defines)
    {
        var navy = defines.NNavy;
        var ownDetection = own.All.Select(ship => ship.GetFinalStats().SurfaceDetection).DefaultIfEmpty(0).Max();
        var enemyDetection = enemy.All.Select(ship => ship.GetFinalStats().SurfaceDetection).DefaultIfEmpty(0).Max();
        var bonus = (ownDetection - enemyDetection) * navy.RELATIVE_SURFACE_DETECTION_TO_POSITIONING_FACTOR;
        return Math.Clamp(bonus, 0, navy.MAX_POSITIONING_BONUS_FROM_SURFACE_DETECTION);
    }

    // S-POS-SCREEN [DEFINE]: share of each ship's screening contribution kept at this positioning.
    public static double ScreeningContributionFactor(double positioning, GameDefines defines)
    {
        return 1.0 - defines.NNavy.SCREENING_EFFICIENCY_PENALTY_ON_MINIMUM_POSITIONING * (1.0 - positioning);
    }

    // S-POS-DMG [DEFINE]: damage multiplier at this positioning.
    public static double DamageFactor(double positioning, GameDefines defines)
    {
        return 1.0 - defines.NNavy.DAMAGE_PENALTY_ON_MINIMUM_POSITIONING * (1.0 - positioning);
    }
}
