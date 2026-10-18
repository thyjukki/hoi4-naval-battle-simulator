using NavySimulator.Engine.Defines;

namespace NavySimulator.Engine.Battles.Mechanics;

/// <summary>
/// S-XP: ship experience levels, their damage modifier, combat gain, and losses from manpower casualties.
/// Spec: docs/mechanics/surface.md. Most of this is carried over from the previous simulator [LEGACY].
/// </summary>
public static class ExperienceRules
{
    public static int Level(Ship ship, GameDefines defines)
    {
        var normalized = Math.Clamp(ship.Experience / defines.NNavy.NAVY_MAX_XP, 0, 1);
        return defines.Hardcoded.ShipExperienceLevels.Count(threshold => normalized >= threshold);
    }

    public static double ExperienceForLevel(int level, GameDefines defines)
    {
        var levels = defines.Hardcoded.ShipExperienceLevels;
        var clamped = Math.Clamp(level, 0, levels.Length);
        return clamped == 0 ? 0 : levels[clamped - 1] * defines.NNavy.NAVY_MAX_XP;
    }

    public static int MaxLevel(GameDefines defines)
    {
        return defines.Hardcoded.ShipExperienceLevels.Length;
    }

    // [LEGACY] Damage modifier spread linearly from the min to the max value across levels.
    public static double DamageFactor(Ship ship, GameDefines defines)
    {
        var hardcoded = defines.Hardcoded;
        var maxLevel = MaxLevel(defines);

        if (maxLevel == 0)
        {
            return 1.0;
        }

        var step = (hardcoded.ShipExperienceMaxDamageFactor - hardcoded.ShipExperienceMinDamageFactor) / maxLevel;
        return Math.Max(0, 1.0 + hardcoded.ShipExperienceMinDamageFactor + step * Level(ship, defines));
    }

    // [DEFINE + LEGACY formula] Hourly gain for a ship that fired this hour, scaled by the enemy/own ship ratio.
    public static double HourlyGain(int enemyShips, int ownShips, GameDefines defines)
    {
        if (ownShips <= 0)
        {
            return 0;
        }

        var navy = defines.NNavy;
        return navy.UNIT_EXPERIENCE_PER_COMBAT_HOUR * navy.UNIT_EXPERIENCE_SCALE * navy.EXPERIENCE_FACTOR_NON_CARRIER_GAIN *
               enemyShips / ownShips;
    }

    public static void AddExperience(Ship ship, double amount, GameDefines defines)
    {
        ship.Experience = Math.Clamp(ship.Experience + amount, 0, defines.NNavy.NAVY_MAX_XP);
    }

    // [DEFINE + LEGACY formula] Manpower lost in proportion to HP lost, floored at MIN_MANPOWER_RATIO_TO_DROP;
    // experience drops by the lost share of crew times EXPERIENCE_LOSS_FACTOR.
    public static int ApplyManpowerLoss(Ship ship, double hpDamage, double maxHp, GameDefines defines)
    {
        if (hpDamage <= 0 || ship.MaxManpower <= 0 || maxHp <= 0)
        {
            return 0;
        }

        var navy = defines.NNavy;
        var manpowerBefore = ship.CurrentManpower;
        var loss = (int)Math.Round(hpDamage / maxHp * ship.MaxManpower * navy.MANPOWER_LOSS_RATIO_ON_STR_LOSS);
        var floor = (int)Math.Round(ship.MaxManpower * navy.MIN_MANPOWER_RATIO_TO_DROP);
        ship.CurrentManpower = Math.Max(Math.Min(floor, manpowerBefore), manpowerBefore - loss);
        var lost = manpowerBefore - ship.CurrentManpower;

        if (lost > 0 && manpowerBefore > 0)
        {
            var lostShare = lost / (double)manpowerBefore;
            ship.Experience = Math.Max(0, ship.Experience - ship.Experience * lostShare * navy.EXPERIENCE_LOSS_FACTOR);
        }

        return lost;
    }

    public static void ApplyDailyManpowerRecovery(Ship ship, GameDefines defines)
    {
        if (ship.IsSunk || ship.CurrentManpower >= ship.MaxManpower)
        {
            return;
        }

        var recovery = (int)Math.Round(ship.MaxManpower * defines.NNavy.DAILY_MANPOWER_GAIN_RATIO);
        ship.CurrentManpower = Math.Min(ship.MaxManpower, ship.CurrentManpower + recovery);
    }
}
