using NavySimulator.Engine.Defines;
using NavySimulator.Engine.Environment;

namespace NavySimulator.Engine.Battles.Mechanics;

/// <summary>
/// S-POS: positioning (0..1), computed for both sides at the start of every hour (0x161b960). Spec: docs/mechanics/surface.md.
/// Not modelled (no inputs for them): the defender's initial-phase modifier, the penalty for ships joining a running
/// battle, and the convoy-escort dominance bonus.
/// </summary>
public static class PositioningRules
{
    public static double Calculate(
        BattleLines own,
        BattleLines enemy,
        GameDefines defines,
        double ownCarrierAir = 0,
        (double A, double B) enemyCarrierAir = default,
        double positioningModifier = 0,
        double enemyFleetSizePenaltyFactor = 0,
        BattleEnvironment environment = default)
    {
        // S-POS-MOD [RE] 0x161bb4c: the side's positioning modifier (country: doctrines, spirits; admiral) joins the sum
        // before the clamp.
        var positioning = defines.NNavy.BASE_POSITIONING + positioningModifier;
        positioning -= FleetSizePenalty(own.Count, enemy.Count, defines, enemyFleetSizePenaltyFactor);
        positioning -= CarrierRatioPenalty(ownCarrierAir, enemyCarrierAir.A, enemyCarrierAir.B, defines);
        positioning += DetectionBonus(own, enemy, defines, environment);
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

    // S-POS-SIZE [RE]: every ship in the battle counts 1 (all lines, retreating included). 0x161bfdc: the penalty × (1 +
    // the enemy commanding admiral's naval_enemy_fleet_size_ratio_penalty_factor) before the cap (S-ADMIRAL).
    public static double FleetSizePenalty(int ownShips, int enemyShips, GameDefines defines, double enemyAdmiralFactor = 0)
    {
        var navy = defines.NNavy;

        if (enemyShips <= 0 || ownShips < navy.MIN_SHIPS_FOR_HIGHER_SHIP_RATIO_PENALTY)
        {
            return 0;
        }

        var penalty = (ownShips / (double)enemyShips - 1.0) * navy.HIGHER_SHIP_RATIO_POSITIONING_PENALTY_FACTOR * (1 + enemyAdmiralFactor);
        return Math.Clamp(penalty, 0, navy.MAX_POSITIONING_PENALTY_FROM_HIGHER_SHIP_RATIO);
    }

    // S-POS-DETECT [RE]: summed effective surface detection (0xc3aae0, with naval_detection: the weather's lowers it) of
    // all ships in the battle, difference / 100 * factor, only if positive.
    // [CAL] With damaged parts' penalties (S-STATS-PARTS): a short-circuited radar (−7) takes its ship's detection to 0,
    // and the enemy's logged positioning rose by exactly that share.
    public static double DetectionBonus(BattleLines own, BattleLines enemy, GameDefines defines, BattleEnvironment environment = default)
    {
        var navy = defines.NNavy;
        var ownDetection = own.All.Sum(ship => CombatStatRules.SurfaceDetection(ship, defines, environment));
        var enemyDetection = enemy.All.Sum(ship => CombatStatRules.SurfaceDetection(ship, defines, environment));
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
