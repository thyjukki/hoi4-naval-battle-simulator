using NavySimulator.Engine.Defines;
using NavySimulator.Engine.Random;
using NavySimulator.Engine.Stats;

namespace NavySimulator.Engine.Battles.Air;

/// <summary>
/// A-REGION [RE + LOG]: formulas of the region's air war that the battle's air wings fight in each air hour, before the
/// naval combat hour (0xc5f4c0, 0xf7be70, 0xf862a0, 0xf77360, 0xc4c570, 0xf78160). Spec: docs/mechanics/air.md;
/// finding A-REGION-carrier-wings-in-the-region-air-war.md.
/// </summary>
public static class RegionAirWarRules
{
    /// <summary>
    /// [RE] 0xf5e890 / 0xf60860: a wing's air combat stats, absolute and divided by AIR_WING_MAX_STATS_*: attack, defence
    /// and agility × (1 + the country's air_attack/defence/agility_factor); speed with the ace's speed bonus. Region
    /// weather, night, home defence / power projection and mission efficiency modifiers are not modelled (0).
    /// </summary>
    public readonly record struct WingStats(double Attack, double Defence, double Agility, double Speed, double AttackShare,
        double DefenceShare, double AgilityShare, double SpeedShare);

    public static WingStats Stats(AirWing wing, GameDefines defines, ModifierSet? country = null)
    {
        var air = defines.NAir;
        var modifiers = country ?? ModifierSet.Empty;
        var stats = wing.Plane.Stats;
        var attack = stats.AirAttack * (1 + modifiers.Get(StaticModifiers.AirAttackFactor));
        var defence = stats.AirDefense * (1 + modifiers.Get(StaticModifiers.AirDefenceFactor));
        var agility = stats.Agility * (1 + modifiers.Get(StaticModifiers.AirAgilityFactor));
        var speed = stats.Speed * (1 + wing.AceBonus(StaticModifiers.AirMaximumSpeedFactor, defines));
        return new WingStats(attack, defence, agility, speed, attack / air.AIR_WING_MAX_STATS_ATTACK, defence / air.AIR_WING_MAX_STATS_DEFENCE,
            agility / air.AIR_WING_MAX_STATS_AGILITY, speed / air.AIR_WING_MAX_STATS_SPEED);
    }

    /// <summary>
    /// [RE] 0xf862a0: the planes one attacker shoots down of one defender, before the cap: the damage share × the
    /// attacker's attack × COMBAT_DAMAGE_STATS_MULTILPIER, less the defender's agility edge, plus the attacker's speed edge
    /// and its top-speed bonus, then x = trunc(trunc(scale × damage) × 0.01) / the defender's defence, at least 0.001.
    /// <paramref name="bothCarriers"/> uses COMBAT_DAMAGE_SCALE_CARRIER, else COMBAT_DAMAGE_SCALE.
    /// </summary>
    public static double ExpectedLosses(double potential, WingStats attacker, WingStats defender, bool bothCarriers, GameDefines defines)
    {
        var air = defines.NAir;
        var damage = potential * air.COMBAT_DAMAGE_STATS_MULTILPIER;
        var reduction = attacker.AgilityShare < defender.AgilityShare && attacker.AgilityShare > 0
            ? (Math.Min(defender.AgilityShare / attacker.AgilityShare, air.BIGGEST_AGILITY_FACTOR_DIFF) - 1) *
              FixedPoint.Truncate(damage * air.COMBAT_BETTER_AGILITY_DAMAGE_REDUCTION)
            : 0;
        var increase = attacker.Speed > defender.Speed && defender.Speed > 0
            ? (Math.Min(attacker.SpeedShare / defender.SpeedShare, air.BIGGEST_SPEED_FACTOR_DIFF) - 1) *
              FixedPoint.Truncate(damage * air.COMBAT_BETTER_SPEED_DAMAGE_INCREASE) +
              FixedPoint.Truncate(Math.Truncate(attacker.Speed / 100) * air.TOP_SPEED_DAMAGE_BONUS_FACTOR) * damage
            : 0;
        damage += increase - reduction;
        var scale = bothCarriers ? air.COMBAT_DAMAGE_SCALE_CARRIER : air.COMBAT_DAMAGE_SCALE;
        return defender.Defence > 0
            ? Math.Max(0.001, FixedPoint.Truncate(FixedPoint.Truncate(scale * damage) * defines.Hardcoded.AirCombatLossScale) / defender.Defence)
            : double.PositiveInfinity;
    }

    /// <summary>[RE] 0xf862a0: the planes lost, at most <paramref name="cap"/>; below it the fraction is rolled.</summary>
    public static int Losses(double expected, int cap, IRandomSource random)
    {
        if (cap <= 0)
        {
            return 0;
        }

        if (expected >= cap)
        {
            return cap;
        }

        var whole = (int)Math.Floor(expected);
        return whole + (random.NextDouble(RollKind.RegionAirLoss) < expected - whole ? 1 : 0);
    }

    /// <summary>
    /// [RE] 0xf7be70: the disruption an attacker of <paramref name="planes"/> effective planes spreads over the strike
    /// missions it meets: trunc(DISRUPTION_FACTOR(_CARRIER) × planes × (1 + speed × DISRUPTION_SPEED_FACTOR) × (1 + agility
    /// × DISRUPTION_AGILITY_FACTOR) × (1 + attack × DISRUPTION_ATTACK_FACTOR) × (1 + detection × DISRUPTION_DETECTION_FACTOR)).
    /// </summary>
    public static double Disruption(int planes, WingStats attacker, bool bothCarriers, GameDefines defines)
    {
        var air = defines.NAir;
        var potential = planes * (1 + attacker.SpeedShare * air.DISRUPTION_SPEED_FACTOR) * (1 + attacker.AgilityShare * air.DISRUPTION_AGILITY_FACTOR) *
                        (1 + attacker.AttackShare * air.DISRUPTION_ATTACK_FACTOR) *
                        (1 + defines.Hardcoded.RegionAirDetection * air.DISRUPTION_DETECTION_FACTOR);
        return FixedPoint.Truncate(Math.Max(0, (bothCarriers ? air.DISRUPTION_FACTOR_CARRIER : air.DISRUPTION_FACTOR) * potential));
    }

    /// <summary>[RE] 0xf77360: an air superiority mission's escort value, which cancels its side's disruption (0xc4c570).</summary>
    public static double Escort(int planes, WingStats wing, GameDefines defines, ModifierSet? country = null)
    {
        var air = defines.NAir;
        return planes * (1 + wing.SpeedShare * air.ESCORT_SPEED_FACTOR) * (1 + wing.AgilityShare * air.ESCORT_AGILITY_FACTOR) *
               (1 + wing.AttackShare * air.ESCORT_ATTACK_FACTOR) * (1 + defines.Hardcoded.RegionAirDetection * air.DISRUPTION_DETECTION_FACTOR) *
               air.ESCORT_FACTOR * (1 + (country ?? ModifierSet.Empty).Get(StaticModifiers.AirEscortEfficiency));
    }

    /// <summary>
    /// [RE + LOG] 0xf78160: the mission efficiency a disrupted wing strikes with, clamp(1 − trunc(sqrt(disruption / den) ×
    /// 0.316228), 0.001, 1), den = planes × (1 + defence × DISRUPTION_DEFENCE_DEFENCE_FACTOR) × (1 + attack ×
    /// DISRUPTION_DEFENCE_ATTACK_FACTOR) × (1 + speed × DISRUPTION_DEFENCE_SPEED_FACTOR) with the wing's shares. [LOG] It
    /// matched the game's strike averages of disrupted carrier bombers (0.67 at disruption 14.4) and land bombers (0.956).
    /// </summary>
    public static double StrikeEfficiency(double disruption, double planes, AirWing wing, GameDefines defines)
    {
        if (disruption <= 0 || planes <= 0)
        {
            return 1;
        }

        var air = defines.NAir;
        var stats = Stats(wing, defines);

        if (stats.DefenceShare <= 0)
        {
            return 1;
        }

        var den = planes * (1 + stats.DefenceShare * air.DISRUPTION_DEFENCE_DEFENCE_FACTOR) * (1 + stats.AttackShare * air.DISRUPTION_DEFENCE_ATTACK_FACTOR) *
                  (1 + stats.SpeedShare * air.DISRUPTION_DEFENCE_SPEED_FACTOR);
        return Math.Clamp(1 - FixedPoint.Truncate(Math.Sqrt(disruption / den) * defines.Hardcoded.DisruptionEfficiencyScale), 0.001, 1);
    }
}
