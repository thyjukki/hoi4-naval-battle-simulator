using NavySimulator.Engine.Defines;

namespace NavySimulator.Engine.Battles.Mechanics;

/// <summary>
/// S-POS: positioning (0..1), computed for both sides at the start of every hour (0x161b960). Spec: docs/mechanics/surface.md.
/// Not modelled (no inputs for them): admiral/country positioning modifiers, the carrier-ratio penalty (needs air),
/// the penalty for ships joining a running battle, and the convoy-escort dominance bonus.
/// </summary>
public static class PositioningRules
{
    public static double Calculate(
        BattleLines own,
        BattleLines enemy,
        GameDefines defines,
        double ownCarrierAir = 0,
        (double A, double B) enemyCarrierAir = default)
    {
        var positioning = defines.NNavy.BASE_POSITIONING;
        positioning -= FleetSizePenalty(own.Count, enemy.Count, defines);
        positioning -= CarrierRatioPenalty(ownCarrierAir, enemyCarrierAir.A, enemyCarrierAir.B, defines);
        positioning += DetectionBonus(own, enemy, defines);
        return Math.Clamp(positioning, 0, 1);
    }

    // S-POS-CARRIER [RE] 0x161c217: the side with weaker carrier air (sortie efficiency x planes) loses up to
    // MAX_CARRIER_RATIO_POSITIONING_PENALTY_FACTOR, scaled by the enemy's average deck fill.
    public static double CarrierRatioPenalty(double ownCarrierAir, double enemyCarrierAir, double enemyDeckFill, GameDefines defines)
    {
        var navy = defines.NNavy;

        if (enemyCarrierAir <= 0)
        {
            return 0;
        }

        var penalty = (1.0 - ownCarrierAir / enemyCarrierAir) * navy.HIGHER_CARRIER_RATIO_POSITIONING_PENALTY_FACTOR * enemyDeckFill;
        return Math.Clamp(penalty, 0, navy.MAX_CARRIER_RATIO_POSITIONING_PENALTY_FACTOR);
    }

    // S-POS-SIZE [RE]: every ship in the battle counts 1 (all lines, retreating included).
    public static double FleetSizePenalty(int ownShips, int enemyShips, GameDefines defines)
    {
        var navy = defines.NNavy;

        if (enemyShips <= 0 || ownShips < navy.MIN_SHIPS_FOR_HIGHER_SHIP_RATIO_PENALTY)
        {
            return 0;
        }

        var penalty = (ownShips / (double)enemyShips - 1.0) * navy.HIGHER_SHIP_RATIO_POSITIONING_PENALTY_FACTOR;
        return Math.Clamp(penalty, 0, navy.MAX_POSITIONING_PENALTY_FROM_HIGHER_SHIP_RATIO);
    }

    // S-POS-DETECT [RE]: summed surface detection of all ships in the battle, difference / 100 * factor, only if positive.
    // [CAL] Damaged parts do not lower it: after radar short circuits, 110 of 110 logged side-hours kept the undamaged
    // detection (S-STATS-PARTS).
    public static double DetectionBonus(BattleLines own, BattleLines enemy, GameDefines defines)
    {
        var navy = defines.NNavy;
        var ownDetection = own.All.Sum(ship => ship.GetFinalStats().SurfaceDetection);
        var enemyDetection = enemy.All.Sum(ship => ship.GetFinalStats().SurfaceDetection);
        var bonus = (ownDetection - enemyDetection) / defines.Hardcoded.DetectionPositioningDivisor *
                    navy.RELATIVE_SURFACE_DETECTION_TO_POSITIONING_FACTOR;
        return bonus > 0 ? Math.Min(bonus, navy.MAX_POSITIONING_BONUS_FROM_SURFACE_DETECTION) : 0;
    }

    // S-POS-DMG [RE]: penalty subtracted inside the additive damage multiplier (see DamageRules.DamageMultiplier).
    public static double DamagePenalty(double positioning, GameDefines defines)
    {
        return defines.NNavy.DAMAGE_PENALTY_ON_MINIMUM_POSITIONING * (1.0 - positioning);
    }
}
