using NavySimulator.Engine.Defines;
using NavySimulator.Engine.Random;

namespace NavySimulator.Engine.Battles.Mechanics;

/// <summary>
/// S-RETREAT: when ships try to leave the battle and how fast they get away. Spec: docs/mechanics/surface.md.
/// The decision threshold and escape-speed formula are not documented anywhere; both need reverse-engineering.
/// </summary>
public static class RetreatRules
{
    // [DEFINE + ASSUMED mapping] HP ratio under which a ship considers retreating, by repair priority.
    public static double HpThreshold(RetreatPriority priority, GameDefines defines)
    {
        var navy = defines.NNavy;
        return priority switch
        {
            RetreatPriority.Low => navy.REPAIR_AND_RETURN_PRIO_LOW_COMBAT,
            RetreatPriority.High => navy.REPAIR_AND_RETURN_PRIO_HIGH_COMBAT,
            _ => navy.REPAIR_AND_RETURN_PRIO_MEDIUM_COMBAT
        };
    }

    public static bool TryStartRetreat(Ship ship, int hour, RetreatPriority priority, GameDefines defines, IRandomSource random)
    {
        var navy = defines.NNavy;

        if (ship.CurrentStatus != ShipStatus.Active || ship.IsSunk || hour < navy.COMBAT_MIN_DURATION)
        {
            return false;
        }

        var maxHp = ship.GetFinalStats().Hp;

        if (maxHp <= 0 || ship.CurrentHP / maxHp >= HpThreshold(priority, defines))
        {
            return false;
        }

        if (random.NextDouble() >= navy.COMBAT_RETREAT_DECISION_CHANCE)
        {
            return false;
        }

        ship.CurrentStatus = ShipStatus.Retreating;
        ship.AttemptedRetreat = true;
        ship.RetreatProgress = 0;
        return true;
    }

    // [DEFINE + LEGACY formula] Escape speed per day; progress accumulates per hour and the ship escapes at 1.
    public static double EscapeSpeedPerDay(Ship ship, int hour, ScreeningSummary ownScreening, GameDefines defines)
    {
        var navy = defines.NNavy;
        var hardcoded = defines.Hardcoded;
        var baseSpeed = ship.Role == ShipRole.Submarine ? navy.ESCAPE_SPEED_SUB_BASE : navy.BASE_ESCAPE_SPEED;
        var combatDays = hour / 24;
        var speed = baseSpeed +
                    ship.GetFinalStats().Speed * navy.SPEED_TO_ESCAPE_SPEED / 100.0 +
                    Math.Min(combatDays * navy.ESCAPE_SPEED_PER_COMBAT_DAY, navy.MAX_ESCAPE_SPEED_FROM_COMBAT_DURATION);

        // [WIKI] Relative bonuses from screening (battle line and carrier group) and carrier screening (carriers, convoys).
        var bonus = 0.0;

        if (ship.Role is ShipRole.Capital or ShipRole.Carrier or ShipRole.Convoy)
        {
            bonus += hardcoded.ScreeningRetreatSpeedBonus * ownScreening.ScreeningEfficiency;
        }

        if (ship.Role is ShipRole.Carrier or ShipRole.Convoy)
        {
            bonus += hardcoded.CarrierScreeningRetreatSpeedBonus * ownScreening.CarrierScreeningEfficiency;
        }

        return speed * (1.0 + bonus);
    }

    public static bool AdvanceRetreat(Ship ship, int hour, ScreeningSummary ownScreening, GameDefines defines)
    {
        if (ship.CurrentStatus != ShipStatus.Retreating || ship.IsSunk)
        {
            return false;
        }

        ship.RetreatProgress += EscapeSpeedPerDay(ship, hour, ownScreening, defines) / 24.0;

        if (ship.RetreatProgress < 1.0)
        {
            return false;
        }

        ship.CurrentStatus = ShipStatus.Retreated;
        return true;
    }
}
