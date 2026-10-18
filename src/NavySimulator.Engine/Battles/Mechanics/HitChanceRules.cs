using NavySimulator.Engine.Defines;

namespace NavySimulator.Engine.Battles.Mechanics;

/// <summary>
/// S-HIT [RE] (0x1970b48): hit = max(base * profileFactor * crewFactor * modifierSum, COMBAT_MIN_HIT_CHANCE), with no upper
/// cap; the shot hits when U &lt; hit. Spec: docs/mechanics/surface.md.
/// </summary>
public static class HitChanceRules
{
    public static HitChanceBreakdown Calculate(
        Ship shooter,
        Ship target,
        WeaponSlot slot,
        ScreeningSummary shooterScreening,
        double darkness,
        GameDefines defines)
    {
        var navy = defines.NNavy;
        var againstSubmarine = target.Role == ShipRole.Submarine;
        var baseChance = navy.COMBAT_BASE_HIT_CHANCE * (againstSubmarine ? navy.DEPTH_CHARGES_HIT_CHANCE_MULT : 1.0);
        var shipProfile = ShipHitProfile(target, defines);
        var weaponProfile = WeaponHitProfile(slot, againstSubmarine, defines);
        var ratio = shipProfile / weaponProfile;
        var profileFactor = Math.Clamp(ratio * ratio, navy.MIN_HIT_PROFILE_MULT, 1.0);
        var modifier = CrewFactor(shooter, defines) * ModifierSum(shooter, slot, shooterScreening, darkness, defines);
        var final = Math.Max(baseChance * profileFactor * modifier, navy.COMBAT_MIN_HIT_CHANCE);

        return new HitChanceBreakdown(baseChance, shipProfile, weaponProfile, profileFactor, modifier, final);
    }

    // S-HIT-SHIP [RE] 0xc37006: 100 * visibility / (max(0.5 * speed, 0.1) + 20); convoys use CONVOY_HIT_PROFILE.
    // naval_visibility_factor and the per-ship hit profile multiplier are not modelled (1.0).
    public static double ShipHitProfile(Ship target, GameDefines defines)
    {
        var navy = defines.NNavy;

        if (target.Role == ShipRole.Convoy)
        {
            return navy.CONVOY_HIT_PROFILE;
        }

        var stats = target.GetFinalStats();
        var visibility = Math.Max(0, target.Role == ShipRole.Submarine ? stats.SubVisibility : stats.SurfaceVisibility);
        var speedTerm = Math.Max(stats.Speed * navy.HIT_PROFILE_SPEED_FACTOR, defines.Hardcoded.MinHitProfileSpeedTerm);
        return visibility * navy.HIT_PROFILE_MULT / (speedTerm + navy.HIT_PROFILE_SPEED_BASE);
    }

    // S-HIT-WEAPON [RE]: GUN_HIT_PROFILES[slot] { heavy, torpedo, light }; the light slot against a submarine (depth
    // charges) uses DEPTH_CHARGES_HIT_PROFILE.
    public static double WeaponHitProfile(WeaponSlot slot, bool againstSubmarine, GameDefines defines)
    {
        var navy = defines.NNavy;
        return slot == WeaponSlot.Light && againstSubmarine ? navy.DEPTH_CHARGES_HIT_PROFILE : navy.GUN_HIT_PROFILES[(int)slot];
    }

    // [RE] Missing organization and manpower of the shooter, added into one factor and floored at 0.
    public static double CrewFactor(Ship shooter, GameDefines defines)
    {
        var navy = defines.NNavy;
        var stats = shooter.GetFinalStats();
        var orgRatio = stats.Organization > 0 ? Math.Clamp(shooter.CurrentOrganization / stats.Organization, 0, 1) : 1;
        var manpowerRatio = shooter.MaxManpower > 0 ? Math.Clamp(shooter.CurrentManpower / (double)shooter.MaxManpower, 0, 1) : 1;
        return Math.Max(0, 1.0 + navy.COMBAT_LOW_ORG_HIT_CHANCE_PENALTY * (1.0 - orgRatio) +
                           navy.COMBAT_LOW_MANPOWER_HIT_CHANCE_PENALTY * (1.0 - manpowerRatio));
    }

    /// <summary>
    /// S-HIT-MODS [RE]: 1 + naval_hit_chance + the shooter's hit chance factor for the slot. naval_hit_chance includes
    /// the static modifier <c>screening_bonus</c> scaled by screening for the battle line and carrier line, and the
    /// <c>night</c> modifier scaled by darkness. Not clamped.
    /// </summary>
    public static double ModifierSum(Ship shooter, WeaponSlot slot, ScreeningSummary shooterScreening, double darkness, GameDefines defines)
    {
        var stats = shooter.GetFinalStats();
        var modifiers = defines.StaticModifiers;
        var navalHitChance = darkness * modifiers.Get(StaticModifiers.Night, StaticModifiers.NavalHitChance);

        if (TargetingRules.LineOf(shooter) is 2 or 3)
        {
            navalHitChance += shooterScreening.ScreeningEfficiency *
                              modifiers.Get(StaticModifiers.ScreeningBonus, StaticModifiers.NavalHitChance);
        }

        if (TargetingRules.LineOf(shooter) == 3)
        {
            navalHitChance += ScreeningRules.CombinedCarrierScreening(shooterScreening) *
                              modifiers.Get(StaticModifiers.CapitalScreeningBonus, StaticModifiers.NavalHitChance);
        }

        var weaponFactor = slot switch
        {
            WeaponSlot.Heavy => stats.HeavyHitChanceFactor,
            WeaponSlot.Torpedo => stats.TorpedoHitChanceFactor,
            _ => stats.LightHitChanceFactor
        };

        return 1.0 + navalHitChance + weaponFactor;
    }
}
