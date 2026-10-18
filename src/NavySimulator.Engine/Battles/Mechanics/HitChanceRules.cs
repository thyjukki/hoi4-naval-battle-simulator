using NavySimulator.Engine.Defines;

namespace NavySimulator.Engine.Battles.Mechanics;

/// <summary>
/// S-HIT: hit chance = max(base * modifiers * min((shipProfile / weaponProfile)^2, 1), COMBAT_MIN_HIT_CHANCE).
/// Spec: docs/mechanics/surface.md.
/// </summary>
public static class HitChanceRules
{
    public static HitChanceBreakdown Calculate(
        Ship shooter,
        Ship target,
        WeaponType weapon,
        ScreeningSummary shooterScreening,
        bool isNight,
        GameDefines defines,
        BattleRules rules)
    {
        var navy = defines.NNavy;
        var baseChance = BaseHitChance(weapon, defines);
        var shipProfile = ShipHitProfile(target, defines);
        var weaponProfile = WeaponHitProfile(weapon, defines);
        var profileFactor = Math.Max(navy.MIN_HIT_PROFILE_MULT, Math.Min(Math.Pow(shipProfile / weaponProfile, 2), 1.0));
        var modifier = Modifier(shooter, weapon, shooterScreening, isNight && rules.NightEnabled, defines);
        var final = Math.Max(baseChance * modifier * profileFactor, navy.COMBAT_MIN_HIT_CHANCE);

        return new HitChanceBreakdown(baseChance, shipProfile, weaponProfile, profileFactor, modifier, final);
    }

    // S-HIT-BASE [DEFINE]
    public static double BaseHitChance(WeaponType weapon, GameDefines defines)
    {
        var navy = defines.NNavy;
        return weapon == WeaponType.DepthCharge
            ? navy.COMBAT_BASE_HIT_CHANCE * navy.DEPTH_CHARGES_HIT_CHANCE_MULT
            : navy.COMBAT_BASE_HIT_CHANCE;
    }

    // S-HIT-SHIP [DEFINE + WIKI]: 100 * visibility / (0.5 * speed + 20); convoys use a fixed profile, subs their sub visibility.
    public static double ShipHitProfile(Ship target, GameDefines defines)
    {
        var navy = defines.NNavy;

        if (target.Role == ShipRole.Convoy)
        {
            return navy.CONVOY_HIT_PROFILE;
        }

        var stats = target.GetFinalStats();
        var visibility = target.Role == ShipRole.Submarine ? stats.SubVisibility : stats.SurfaceVisibility;
        var denominator = navy.HIT_PROFILE_SPEED_FACTOR * stats.Speed + navy.HIT_PROFILE_SPEED_BASE;
        return denominator <= 0 ? navy.HIT_PROFILE_MULT : navy.HIT_PROFILE_MULT * visibility / denominator;
    }

    // S-HIT-WEAPON [DEFINE]: GUN_HIT_PROFILES is ordered { heavy, torpedo, light }.
    public static double WeaponHitProfile(WeaponType weapon, GameDefines defines)
    {
        var navy = defines.NNavy;
        return weapon switch
        {
            WeaponType.Heavy => navy.GUN_HIT_PROFILES[0],
            WeaponType.Torpedo => navy.GUN_HIT_PROFILES[1],
            WeaponType.Light => navy.GUN_HIT_PROFILES[2],
            _ => navy.DEPTH_CHARGES_HIT_PROFILE
        };
    }

    // S-HIT-MODS: each modifier multiplies the chance [ASSUMED: multiplicative stacking].
    public static double Modifier(Ship shooter, WeaponType weapon, ScreeningSummary shooterScreening, bool isNight, GameDefines defines)
    {
        var navy = defines.NNavy;
        var stats = shooter.GetFinalStats();
        var modifier = 1.0 + weapon switch
        {
            WeaponType.Light => stats.LightHitChanceFactor,
            WeaponType.Heavy => stats.HeavyHitChanceFactor,
            WeaponType.Torpedo => stats.TorpedoHitChanceFactor,
            _ => stats.DepthChargeHitChanceFactor
        };

        // [DEFINE] Linear penalty with missing organization and manpower.
        if (stats.Organization > 0)
        {
            modifier *= 1.0 + navy.COMBAT_LOW_ORG_HIT_CHANCE_PENALTY * (1.0 - Math.Clamp(shooter.CurrentOrganization / stats.Organization, 0, 1));
        }

        if (shooter.MaxManpower > 0)
        {
            modifier *= 1.0 + navy.COMBAT_LOW_MANPOWER_HIT_CHANCE_PENALTY * (1.0 - shooter.CurrentManpower / (double)shooter.MaxManpower);
        }

        // [WIKI] Screening gives up to +40% hit chance to the battle line and carrier group.
        if (shooter.Role is ShipRole.Capital or ShipRole.Carrier or ShipRole.Convoy)
        {
            modifier *= 1.0 + defines.Hardcoded.ScreeningHitChanceBonus * shooterScreening.ScreeningEfficiency;
        }

        // [ASSUMED] Night penalty value.
        if (isNight)
        {
            modifier *= 1.0 + defines.Hardcoded.NightHitChanceFactor;
        }

        return Math.Max(0, modifier);
    }
}
