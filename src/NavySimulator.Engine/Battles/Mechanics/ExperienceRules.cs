using NavySimulator.Engine.Defines;
using NavySimulator.Engine.Environment;

namespace NavySimulator.Engine.Battles.Mechanics;

/// <summary>
/// S-XP [RE]: ship experience levels, their combat modifiers, hourly combat gain, and losses from damage; S-MANPOWER [RE]:
/// the daily manpower refill.
/// Spec: docs/mechanics/surface.md.
/// </summary>
public static class ExperienceRules
{
    // [RE] level = number of NMilitary.UNIT_EXP_LEVELS thresholds reached by xp / NAVY_MAX_XP (0..4 in vanilla).
    public static int Level(Ship ship, GameDefines defines)
    {
        return Level(ship.Experience, defines);
    }

    public static int Level(double experience, GameDefines defines)
    {
        var ratio = Math.Clamp(experience / defines.NNavy.NAVY_MAX_XP, 0, 1);
        return defines.NMilitary.UNIT_EXP_LEVELS.Count(threshold => ratio >= threshold);
    }

    public static int MaxLevel(GameDefines defines)
    {
        return defines.NMilitary.UNIT_EXP_LEVELS.Length;
    }

    public static double ExperienceForLevel(int level, GameDefines defines)
    {
        var levels = defines.NMilitary.UNIT_EXP_LEVELS;
        var clamped = Math.Clamp(level, 0, levels.Length);
        return clamped == 0 ? 0 : levels[clamped - 1] * defines.NNavy.NAVY_MAX_XP;
    }

    /// <summary>
    /// [RE] 0xc3ce50: share of the static modifier <c>ship_experience_bonus_max</c> (above ARMY_EXP_BASE_LEVEL) or
    /// <c>ship_experience_malus_min</c> (below it) applied to the ship, as (block, scale). [CAL] The level is the one
    /// at the start of the combat hour (<see cref="Ship.LevelExperience"/>): in a logged TFB battle the attack in all
    /// 8,572 shot profiles matched the shooter's start-of-hour level, while its end-of-hour level differed 185 times.
    /// </summary>
    public static (string Block, double Scale) LevelModifier(Ship ship, GameDefines defines)
    {
        var level = Level(ship.LevelExperience, defines);
        var baseLevel = (int)defines.NMilitary.ARMY_EXP_BASE_LEVEL;
        var maxLevel = MaxLevel(defines);

        if (level < baseLevel && baseLevel > 0)
        {
            return (StaticModifiers.ShipExperienceMalusMin, (baseLevel - level) / (double)baseLevel);
        }

        if (level > baseLevel && maxLevel > baseLevel)
        {
            return (StaticModifiers.ShipExperienceBonusMax, (level - baseLevel) / (double)(maxLevel - baseLevel));
        }

        return (StaticModifiers.ShipExperienceBonusMax, 0);
    }

    /// <summary>[RE] naval_damage_factor the ship gets from its experience level.</summary>
    public static double NavalDamageFactor(Ship ship, GameDefines defines)
    {
        var (block, scale) = LevelModifier(ship, defines);
        return scale * defines.StaticModifiers.Get(block, StaticModifiers.NavalDamageFactor);
    }

    /// <summary>[RE] naval_defense_factor the ship gets from its experience level.</summary>
    public static double NavalDefenseFactor(Ship ship, GameDefines defines)
    {
        var (block, scale) = LevelModifier(ship, defines);
        return scale * defines.StaticModifiers.Get(block, StaticModifiers.NavalDefenseFactor);
    }

    /// <summary>
    /// [RE] Hourly gain for a ship that fired this hour: UNIT_EXPERIENCE_PER_COMBAT_HOUR * UNIT_EXPERIENCE_SCALE *
    /// (enemy group size + 1) / (own group size + 1) * EXPERIENCE_FACTOR_NON_CARRIER_GAIN, times
    /// EXPERIENCE_FACTOR_CONVOY_ATTACK if its last target was a convoy. Carrier sortie gain needs air (milestone 2).
    /// </summary>
    public static double HourlyGain(int enemyGroupSize, int ownGroupSize, bool lastTargetWasConvoy, GameDefines defines)
    {
        var navy = defines.NNavy;
        var factor = (enemyGroupSize + 1.0) / (ownGroupSize + 1.0) * navy.EXPERIENCE_FACTOR_NON_CARRIER_GAIN;

        if (lastTargetWasConvoy)
        {
            factor *= navy.EXPERIENCE_FACTOR_CONVOY_ATTACK;
        }

        return navy.UNIT_EXPERIENCE_PER_COMBAT_HOUR * navy.UNIT_EXPERIENCE_SCALE * factor;
    }

    /// <summary>
    /// S-XP-GAIN [RE] 0xc3b240: <c>(gain + flat) × (1 + factor)</c>, with flat experience_gain_navy_unit and factor
    /// experience_gain_navy_unit_factor of the country, the pride of the fleet block for the pride itself (S-POTF: factor
    /// +0.25), and the country's experience_gain_&lt;sub-unit&gt;_combat_factor for the ship's sub-unit. No admiral term;
    /// the ship's medals are not modelled.
    /// </summary>
    public static double ModifiedGain(Ship ship, double gain, GameDefines defines)
    {
        var country = ship.CountryModifiers;
        var flat = country.Get(StaticModifiers.ExperienceGainNavyUnit);
        var factor = country.Get(StaticModifiers.ExperienceGainNavyUnitFactor) + country.Get(SubUnitCombatFactor(ship.Design.ShipType.ID));

        if (ship.IsPrideOfTheFleet)
        {
            flat += defines.StaticModifiers.Get(StaticModifiers.PrideOfTheFleet, StaticModifiers.ExperienceGainNavyUnit);
            factor += defines.StaticModifiers.Get(StaticModifiers.PrideOfTheFleet, StaticModifiers.ExperienceGainNavyUnitFactor);
        }

        return (gain + flat) * (1 + factor);
    }

    /// <summary>[RE] 0x1018080: the country modifier a sub-unit's combat experience gain reads, e.g. experience_gain_destroyer_combat_factor.</summary>
    public static string SubUnitCombatFactor(string subUnit)
    {
        return $"experience_gain_{subUnit}_combat_factor";
    }

    public static void AddExperience(Ship ship, double amount, GameDefines defines)
    {
        ship.Experience = Math.Clamp(ship.Experience + amount, 0, defines.NNavy.NAVY_MAX_XP);
    }

    /// <summary>
    /// [RE] 0xc3e0e0, before HP is reduced: experience keeps the share of current HP the hit leaves; manpower drops in
    /// proportion to strDamage / maxHp. strDamage is the unclamped value (it may exceed the remaining HP).
    /// </summary>
    public static int ApplyDamageLosses(Ship ship, double strDamage, GameDefines defines)
    {
        var navy = defines.NNavy;
        var maxHp = Math.Max(ship.GetFinalStats().Hp, 0.001);
        var hpBefore = ship.CurrentHP;

        if (hpBefore > 0)
        {
            var keep = Math.Clamp(hpBefore - strDamage * navy.EXPERIENCE_LOSS_FACTOR, 0, maxHp);
            ship.Experience = Math.Min(ship.Experience * (keep / hpBefore), navy.NAVY_MAX_XP);
        }

        if (strDamage <= 0 || ship.MaxManpower <= 0)
        {
            return 0;
        }

        // [RE] 0xc3e305: the loss ratio is x (1 + navy_casualty_on_hit); none at all when that is <= 0.
        var casualty = 1 + MemberModifierRules.ShipModifier(ship, StaticModifiers.NavyCasualtyOnHit, BattleEnvironment.None, defines);

        if (casualty <= 0)
        {
            return 0;
        }

        var floor = RoundHalfAway(ship.MaxManpower * navy.MIN_MANPOWER_RATIO_TO_DROP);

        if (ship.CurrentManpower <= floor)
        {
            return 0;
        }

        // [CAL] The game keeps strDamage / maxHp in fx5, truncated: rounding the exact ratio misses 48 of 22087 logged
        // losses (fractions just above .5 that the game rounds down), the truncated ratio none.
        var ratio = Math.Floor(strDamage / maxHp * 100000 + 1e-9) / 100000;

        if (casualty != 1)
        {
            ratio = Math.Floor(ratio * casualty * 100000 + 1e-9) / 100000;
        }
        var loss = RoundHalfAway(ship.MaxManpower * ratio * navy.MANPOWER_LOSS_RATIO_ON_STR_LOSS);
        var applied = Math.Min(loss, ship.CurrentManpower - floor);
        ship.CurrentManpower -= applied;
        return applied;
    }

    /// <summary>
    /// S-MANPOWER [RE] 0xc3e4e0 (refill): once a day every ship regains min(deficit, ceil(maxManpower ×
    /// DAILY_MANPOWER_GAIN_RATIO)), in combat too. The ceiling is taken in fx5 as the game does. The national manpower
    /// pool is [ASSUMED] never short.
    /// </summary>
    /// <summary>
    /// S-MANPOWER [RE + CAL]: the task force refill, <c>ceil(maxManpower × DAILY_MANPOWER_GAIN_RATIO)</c> up to the
    /// maximum. It runs in the daily update and, in battle, every time a ship of the task force sinks.
    /// </summary>
    public static int DailyManpowerGain(Ship ship, GameDefines defines)
    {
        var deficit = ship.MaxManpower - ship.CurrentManpower;

        if (deficit <= 0)
        {
            return 0;
        }

        var ratioFx5 = (long)Math.Round(defines.NNavy.DAILY_MANPOWER_GAIN_RATIO * 100000, MidpointRounding.AwayFromZero);
        var step = (int)Math.Max(0, (ship.MaxManpower * ratioFx5 + 99999) / 100000);
        var gain = Math.Min(deficit, step);
        ship.CurrentManpower += gain;
        return gain;
    }

    private static int RoundHalfAway(double value)
    {
        return (int)Math.Round(value, MidpointRounding.AwayFromZero);
    }

    /// <summary>
    /// S-ORG-REGAIN [RE + CAL] 0xc395a0: every ship that is not fighting regains
    /// <c>(SHIP_MORALE_TO_ORG_REGAIN_BASE + naval_morale) × max(0, 1 + naval_morale_factor + dominance)</c> organization an
    /// hour, up to its maximum (a fire, S-CRIT-PART, has naval_morale_factor −0.8). <paramref name="dominance"/> is
    /// NAVAL_DOMINANCE_ORG_RECOVERY for the side with naval dominance (S-DOMINANCE), else 0. Not modelled: a ship value at
    /// +0x120 in the same factor.
    /// </summary>
    public static double HourlyOrganizationRegain(Ship ship, GameDefines defines, double dominance = 0, BattleEnvironment environment = default)
    {
        if (ship.IsSunk || ship.CurrentStatus == ShipStatus.Active)
        {
            return 0;
        }

        var maximum = CombatStatRules.MaxOrganization(ship, defines, environment);
        var regain = (defines.NMilitary.SHIP_MORALE_TO_ORG_REGAIN_BASE + MemberModifierRules.ShipModifier(ship, StaticModifiers.NavalMorale, environment, defines)) *
                     Math.Max(0, 1 + MemberModifierRules.ShipModifier(ship, StaticModifiers.NavalMoraleFactor, environment, defines) + dominance);
        var gain = Math.Max(0, Math.Min(regain, maximum - ship.CurrentOrganization));
        ship.CurrentOrganization += gain;
        return gain;
    }
}
