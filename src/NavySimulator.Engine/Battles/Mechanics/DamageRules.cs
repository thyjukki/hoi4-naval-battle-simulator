using NavySimulator.Engine.Defines;
using NavySimulator.Engine.Random;
using NavySimulator.Engine.Stats;

namespace NavySimulator.Engine.Battles.Mechanics;

/// <summary>
/// S-DMG, S-CRIT and S-DMG-APPLY [RE] (0x1970a90, 0x19740c0, 0xc3e0e0). Spec: docs/mechanics/surface.md.
/// Order: combat attack (with the damage factor) x piercing against combat armor (with the defense factor), x (1 -
/// positioning penalty + damage factor - defense factor), x randomness, x torpedo reduction, split into HP and
/// organization damage, x critical multiplier. The damage and defense factors therefore count twice.
/// </summary>
public static class DamageRules
{
    /// <summary>
    /// The shot profile of a slot against a target. <paramref name="criticalChanceFactor"/> multiplies the critical
    /// chance (<see cref="CriticalChanceFactor"/>); the game applies it in the shot loop, after the logged profile.
    /// </summary>
    public static ShotProfile Profile(Ship shooter, Ship target, WeaponSlot slot, double shooterPositioning, GameDefines defines,
        double criticalChanceFactor = 1.0)
    {
        var navy = defines.NNavy;
        var shooterStats = shooter.GetFinalStats();
        var targetStats = target.GetFinalStats();
        var againstSubmarine = target.Role == ShipRole.Submarine;
        var reliability = Math.Min(targetStats.Reliability, 1);

        double attack;
        var tier = -1;
        var piercingFactor = 1.0;
        double? torpedoFactor = null;
        double criticalChance;
        double criticalMultiplier;

        if (slot == WeaponSlot.Torpedo)
        {
            attack = shooterStats.TorpedoAttack;
            torpedoFactor = 1.0 - targetStats.TorpedoDamageReductionFactor;
            criticalChance = navy.COMBAT_TORPEDO_CRITICAL_CHANCE * Math.Max(0, 1.0 + targetStats.TorpedoEnemyCriticalChanceFactor);
            criticalMultiplier = navy.COMBAT_TORPEDO_CRITICAL_DAMAGE_MULT;
        }
        else
        {
            // Heavy guns do nothing to submarines; the light slot fires depth charges at them. Depth charges still go
            // through the piercing tiers, with the shooter's light piercing against the submarine's armor.
            var piercing = slot == WeaponSlot.Heavy ? shooterStats.HeavyPiercing : shooterStats.LightPiercing;
            attack = slot switch
            {
                WeaponSlot.Heavy => againstSubmarine ? 0 : shooterStats.HeavyAttack,
                _ => againstSubmarine ? shooterStats.DepthChargeAttack * navy.DEPTH_CHARGES_DAMAGE_MULT : shooterStats.LightAttack
            };
            tier = PiercingTier(piercing, CombatArmor(target, targetStats, defines), defines);
            piercingFactor = TierValue(navy.NAVY_PIERCING_THRESHOLD_DAMAGE_VALUES, tier);
            criticalChance = navy.COMBAT_BASE_CRITICAL_CHANCE * (1.0 - reliability) * TierValue(navy.NAVY_PIERCING_THRESHOLD_CRITICAL_VALUES, tier);
            criticalMultiplier = 1.0 + (1.0 - reliability) * navy.COMBAT_CRITICAL_DAMAGE_MULT;
            // COMBAT_ARMOR_PIERCING_DAMAGE_REDUCTION (0 in vanilla, "deprecated") is not modelled.
        }

        criticalChance *= criticalChanceFactor;
        var baseDamage = Math.Max(0, attack * AttackFactor(shooter, defines) * piercingFactor *
                                     DamageMultiplier(shooter, target, shooterPositioning, defines));
        return new ShotProfile(baseDamage, tier, piercingFactor, criticalChance, criticalMultiplier, torpedoFactor,
            slot == WeaponSlot.Light && againstSubmarine);
    }

    /// <summary>
    /// S-CRIT [RE + CAL] 0x19740c0: every critical chance is multiplied by <c>1 + naval_critical_score_chance_factor</c>
    /// of the shooter <c>+ critical_receive_chance</c> of the target (modifier 0x18c), both read through the member
    /// modifier getter. [CAL] TFB's damage_control_1 and _2 (−0.1 each) gave a logged battle 0.8 × the profile's chance.
    /// naval_critical_score_chance_factor_against (a country) is not modelled.
    /// </summary>
    public static double CriticalChanceFactor(Ship shooter, Ship target, ScreeningSummary shooterScreening, ScreeningSummary targetScreening,
        double darkness, GameDefines defines)
    {
        return 1.0 + MemberModifierRules.Get(shooter, StaticModifiers.NavalCriticalScoreChanceFactor, shooterScreening, darkness, defines) +
               MemberModifierRules.Get(target, StaticModifiers.CriticalReceiveChance, targetScreening, darkness, defines);
    }

    /// <summary>
    /// S-STATS-COMBAT [RE + CAL] 0xc34a50: the combat attack getter's factor, max(0, 1 + naval_damage_factor). Only the
    /// experience term is modelled. The hit multiplier adds naval_damage_factor again (logged in-game hits:
    /// docs/reverse-engineering/findings/LOG-ingame-logging.md).
    /// </summary>
    public static double AttackFactor(Ship shooter, GameDefines defines)
    {
        return Math.Max(0, 1.0 + ExperienceRules.NavalDamageFactor(shooter, defines));
    }

    /// <summary>
    /// S-STATS-COMBAT [RE] 0xc34a50 kind 4: armor x max(0, 1 + naval_defense_factor), the armor the piercing tier is read
    /// against. Only the experience term is modelled.
    /// </summary>
    public static double CombatArmor(Ship target, ShipStats targetStats, GameDefines defines)
    {
        return targetStats.Armor * Math.Max(0, 1.0 + ExperienceRules.NavalDefenseFactor(target, defines));
    }

    /// <summary>[RE] max(0, 1 - positioning penalty + shooter naval_damage_factor - target naval_defense_factor).</summary>
    public static double DamageMultiplier(Ship shooter, Ship target, double shooterPositioning, GameDefines defines)
    {
        var multiplier = 1.0 - PositioningRules.DamagePenalty(shooterPositioning, defines) +
                         ExperienceRules.NavalDamageFactor(shooter, defines) -
                         ExperienceRules.NavalDefenseFactor(target, defines);
        return Math.Max(0, multiplier);
    }

    /// <summary>
    /// Damage of one hit against the target's current state. RNG order (as in the game): damage randomness, crit roll
    /// (only when the crit chance and the damage are positive), part-damage roll (only on a crit).
    /// </summary>
    public static ShotDamage RollHit(Ship target, ShotProfile profile, GameDefines defines, BattleRules rules, IRandomSource random)
    {
        var navy = defines.NNavy;
        var randomFactor = RandomFactor(rules.DamageRandomness, navy.COMBAT_DAMAGE_RANDOMNESS, random);
        var damage = Math.Max(0, profile.BaseDamage * randomFactor);

        if (damage <= 0)
        {
            return new ShotDamage(0, 0, 0, profile.PiercingTier, profile.PiercingDamageFactor, false, 1, false, randomFactor);
        }

        var critical = profile.CriticalChance > 0 && random.NextDouble(RollKind.Critical) <= profile.CriticalChance;

        if (profile.TorpedoFactor is { } torpedoFactor)
        {
            damage *= torpedoFactor;
        }

        // S-DMG-APPLY: organization damage scales with the HP the target had already lost before this shot.
        var strDamage = damage * navy.COMBAT_DAMAGE_TO_STR_FACTOR;
        var orgDamage = damage * (1.0 - TargetingRules.HpRatio(target)) * navy.COMBAT_DAMAGE_TO_ORG_FACTOR;
        var partHit = false;
        var multiplier = 1.0;

        if (critical)
        {
            partHit = RollPartDamage(target, defines, random);

            // S-CRIT-PART: a damaged part replaces the critical multiplier with the part's own damage multipliers.
            // No part data is loaded yet, so the game's "no part candidates" path applies: the regular multiplier.
            multiplier = profile.CriticalMultiplier;
            strDamage *= multiplier;
            orgDamage *= multiplier;
        }

        return new ShotDamage(damage, strDamage, orgDamage, profile.PiercingTier, profile.PiercingDamageFactor, critical,
            multiplier, partHit, randomFactor);
    }

    // S-DMG-PIERCE [RE] 0x196fd60: first tier whose threshold piercing/armor reaches; armor 0 counts as ratio 100.
    public static int PiercingTier(double piercing, double armor, GameDefines defines)
    {
        var thresholds = defines.NNavy.NAVY_PIERCING_THRESHOLDS;
        var ratio = armor == 0 ? 100.0 : piercing / armor;

        for (var i = 0; i < thresholds.Length; i++)
        {
            if (ratio >= thresholds[i])
            {
                return i;
            }
        }

        return -1;
    }

    // [RE] When no threshold matches (only possible with modded tables) the game returns table[0] + 1.
    public static double TierValue(double[] table, int tier)
    {
        return tier >= 0 ? table[tier] : table[0] + 1.0;
    }

    // S-DMG-RANDOM [RE]: 1 + R * (U - 0.05).
    public static double RandomFactor(DamageRandomnessModel model, double randomness, IRandomSource random)
    {
        return model == DamageRandomnessModel.Game ? 1.0 + randomness * (random.NextDouble(RollKind.DamageRandomness) - 0.05) : 1.0;
    }

    // S-CRIT-PART [RE]: chance CHANCE_TO_DAMAGE_PART_ON_CRITICAL_HIT / reliability (critical_part_damage_chance_mult = 1);
    // no roll when that is >= 1 or reliability is 0.
    private static bool RollPartDamage(Ship target, GameDefines defines, IRandomSource random)
    {
        if (target.Role == ShipRole.Convoy)
        {
            return false;
        }

        var reliability = Math.Min(target.GetFinalStats().Reliability, 1);
        var chance = defines.NNavy.CHANCE_TO_DAMAGE_PART_ON_CRITICAL_HIT;

        if (reliability <= 0)
        {
            return true;
        }

        chance /= reliability;
        return chance >= 1 || random.NextDouble(RollKind.PartDamage) < chance;
    }

    /// <summary>
    /// [RE] 0xc3e0e0: experience and manpower losses first (from the unclamped HP damage), then HP and organization are
    /// reduced and clamped at 0.
    /// </summary>
    public static AppliedDamage Apply(Ship target, double strDamage, double orgDamage, GameDefines defines)
    {
        var hpBefore = target.CurrentHP;
        var orgBefore = target.CurrentOrganization;
        var manpowerLost = ExperienceRules.ApplyDamageLosses(target, strDamage, defines);

        target.CurrentHP = Math.Clamp(hpBefore - strDamage, 0, target.GetFinalStats().Hp);
        target.CurrentOrganization = Math.Clamp(orgBefore - orgDamage, 0, target.GetFinalStats().Organization);

        return new AppliedDamage(hpBefore - target.CurrentHP, orgBefore - target.CurrentOrganization, manpowerLost,
            target.IsSunk && hpBefore > 0);
    }
}
