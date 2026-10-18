using NavySimulator.Engine.Defines;
using NavySimulator.Engine.Random;

namespace NavySimulator.Engine.Battles.Mechanics;

/// <summary>
/// S-DMG and S-CRIT: damage of a hit and how it is applied to HP, organization, manpower and experience.
/// Spec: docs/mechanics/surface.md.
/// </summary>
public static class DamageRules
{
    public static ShotDamage Calculate(
        Ship shooter,
        Ship target,
        WeaponType weapon,
        double attack,
        double piercing,
        double shooterPositioning,
        GameDefines defines,
        BattleRules rules,
        IRandomSource random)
    {
        var navy = defines.NNavy;
        var targetStats = target.GetFinalStats();
        var ignoresArmor = weapon is WeaponType.Torpedo or WeaponType.DepthCharge;
        var tier = ignoresArmor ? 0 : PiercingTier(piercing, targetStats.Armor, defines);
        var piercingDamageFactor = ignoresArmor ? 1.0 : navy.NAVY_PIERCING_THRESHOLD_DAMAGE_VALUES[tier];

        var critical = RollCritical(weapon, target, tier, defines, random);

        var damage = attack;
        damage *= ExperienceRules.DamageFactor(shooter, defines);
        damage *= piercingDamageFactor;
        damage *= critical.Multiplier;
        damage *= PositioningRules.DamageFactor(shooterPositioning, defines);

        if (weapon == WeaponType.Torpedo)
        {
            damage *= Math.Max(0, 1.0 - targetStats.TorpedoDamageReductionFactor);
        }

        if (weapon == WeaponType.DepthCharge)
        {
            damage *= navy.DEPTH_CHARGES_DAMAGE_MULT;
        }

        var randomFactor = RandomFactor(rules.DamageRandomness, navy.COMBAT_DAMAGE_RANDOMNESS, random);
        damage *= randomFactor;

        return new ShotDamage(Math.Max(0, damage), tier, piercingDamageFactor, critical.IsCritical, critical.Multiplier, critical.IsPartHit, randomFactor);
    }

    // S-DMG-PIERCE [DEFINE]: first tier whose threshold the piercing/armor ratio reaches.
    public static int PiercingTier(double piercing, double armor, GameDefines defines)
    {
        var thresholds = defines.NNavy.NAVY_PIERCING_THRESHOLDS;
        var ratio = armor <= 0 ? double.PositiveInfinity : piercing / armor;

        for (var i = 0; i < thresholds.Length; i++)
        {
            if (ratio >= thresholds[i])
            {
                return i;
            }
        }

        return thresholds.Length - 1;
    }

    // S-DMG-RANDOM: see DamageRandomnessModel.
    public static double RandomFactor(DamageRandomnessModel model, double randomness, IRandomSource random)
    {
        return model switch
        {
            DamageRandomnessModel.WikiDecoded => 1.0 + randomness * (random.NextDouble() - 0.05),
            DamageRandomnessModel.DefineComment => 1.0 - randomness * random.NextDouble(),
            _ => 1.0
        };
    }

    // S-CRIT [DEFINE + WIKI]: torpedoes crit at a flat chance and multiplier; guns scale with the piercing tier and
    // the target's missing reliability. A crit may also damage a part (effects not modelled yet, S-CRIT-PART).
    private static (bool IsCritical, double Multiplier, bool IsPartHit) RollCritical(
        WeaponType weapon,
        Ship target,
        int piercingTier,
        GameDefines defines,
        IRandomSource random)
    {
        var navy = defines.NNavy;
        var targetStats = target.GetFinalStats();
        var reliability = Math.Clamp(targetStats.Reliability, 0, 1);
        double chance;
        double multiplier;

        if (weapon == WeaponType.Torpedo)
        {
            chance = navy.COMBAT_TORPEDO_CRITICAL_CHANCE * (1.0 + targetStats.TorpedoEnemyCriticalChanceFactor);
            multiplier = navy.COMBAT_TORPEDO_CRITICAL_DAMAGE_MULT;
        }
        else
        {
            var tierFactor = weapon == WeaponType.DepthCharge ? 1.0 : navy.NAVY_PIERCING_THRESHOLD_CRITICAL_VALUES[piercingTier];
            chance = navy.COMBAT_BASE_CRITICAL_CHANCE * tierFactor * (1.0 - reliability);
            multiplier = 1.0 + (navy.COMBAT_CRITICAL_DAMAGE_MULT - 1.0) * (1.0 - reliability);
        }

        if (random.NextDouble() >= Math.Clamp(chance, 0, 1))
        {
            return (false, 1.0, false);
        }

        var partChance = navy.CHANCE_TO_DAMAGE_PART_ON_CRITICAL_HIT / Math.Max(reliability, 0.01);
        var isPartHit = random.NextDouble() < Math.Clamp(partChance, 0, 1);
        return (true, multiplier, isPartHit);
    }

    /// <summary>
    /// S-DMG-APPLY [DEFINE + WIKI]: HP takes damage * COMBAT_DAMAGE_TO_STR_FACTOR. Organization takes
    /// damage * COMBAT_DAMAGE_TO_ORG_FACTOR scaled by the share of HP the ship had already lost before the hit
    /// (wiki example: a 100 HP / 50 org ship hit for 50 four times loses 30 HP each time and 0, 15, 30, 45 org).
    /// </summary>
    public static AppliedDamage Apply(Ship target, double damage, double strFactor, double orgFactor, GameDefines defines)
    {
        var maxHp = target.GetFinalStats().Hp;
        var hpBefore = target.CurrentHP;
        var orgBefore = target.CurrentOrganization;

        target.CurrentHP = Math.Max(0, hpBefore - damage * strFactor);
        var hpDamage = hpBefore - target.CurrentHP;

        var missingHpBefore = maxHp <= 0 ? 0 : Math.Clamp(1.0 - hpBefore / maxHp, 0, 1);
        target.CurrentOrganization = Math.Max(0, orgBefore - damage * orgFactor * missingHpBefore);
        var orgDamage = orgBefore - target.CurrentOrganization;

        var manpowerLost = ExperienceRules.ApplyManpowerLoss(target, hpDamage, maxHp, defines);
        return new AppliedDamage(hpDamage, orgDamage, manpowerLost, target.IsSunk && hpBefore > 0);
    }
}
