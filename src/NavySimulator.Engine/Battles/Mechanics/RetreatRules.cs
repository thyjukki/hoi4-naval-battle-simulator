using NavySimulator.Engine.Defines;
using NavySimulator.Engine.Random;

namespace NavySimulator.Engine.Battles.Mechanics;

/// <summary>
/// S-RETREAT and S-RETREAT-SPEED [RE] (0x19717f0, 0x1975280, 0x196fe80). Spec: docs/mechanics/surface.md.
/// Not modelled: the side-wide retreat order, the aggressiveness check that makes outmatched task forces retreat (its
/// strength score 0x1989c60 is not decoded), and the convoy-raiding submarine rule.
/// </summary>
public static class RetreatRules
{
    // [RE] In-combat threshold by task force aggressiveness; capital ships and carriers use CAPITAL_SHIP_COMBAT_RETREAT_MULT of it.
    public static double HpThreshold(Ship ship, Aggressiveness aggressiveness, GameDefines defines)
    {
        var navy = defines.NNavy;
        var threshold = aggressiveness switch
        {
            Aggressiveness.DoNotEngage or Aggressiveness.EngageAtLowRisk => navy.REPAIR_AND_RETURN_PRIO_LOW_COMBAT,
            Aggressiveness.EngageAtMediumRisk => navy.REPAIR_AND_RETURN_PRIO_MEDIUM_COMBAT,
            _ => navy.REPAIR_AND_RETURN_PRIO_HIGH_COMBAT
        };

        return ship.Role is ShipRole.Capital or ShipRole.Carrier ? threshold * navy.CAPITAL_SHIP_COMBAT_RETREAT_MULT : threshold;
    }

    /// <summary>
    /// [RE] Called for an active ship before it fires, from tick COMBAT_MIN_DURATION on. Below the HP threshold it rolls
    /// COMBAT_RETREAT_DECISION_CHANCE; on success it starts retreating and does not fire this hour.
    /// <paramref name="enemyRetreatChance"/> is the enemy commanding admiral's naval_enemy_retreat_chance.
    /// </summary>
    public static bool TryStartRetreat(
        Ship ship,
        int tick,
        Aggressiveness aggressiveness,
        GameDefines defines,
        IRandomSource random,
        double enemyRetreatChance = 0)
    {
        var navy = defines.NNavy;

        if (ship.CurrentStatus != ShipStatus.Active || ship.IsSunk || tick < navy.COMBAT_MIN_DURATION)
        {
            return false;
        }

        if (TargetingRules.HpRatio(ship) >= HpThreshold(ship, aggressiveness, defines))
        {
            return false;
        }

        if (random.NextDouble(RollKind.RetreatDecision) > RetreatDecisionChance(ship, tick, defines, enemyRetreatChance))
        {
            return false;
        }

        ship.CurrentStatus = ShipStatus.Retreating;
        ship.AttemptedRetreat = true;
        ship.RetreatProgress = 0;
        return true;
    }

    /// <summary>
    /// [RE] 0x1975280: COMBAT_RETREAT_DECISION_CHANCE × (1 + naval_retreat_chance) × (1 +
    /// naval_retreat_chance_after_initial_combat, after COMBAT_INITIAL_DURATION) × (1 + the enemy commanding admiral's
    /// naval_enemy_retreat_chance, read from its traits only: container B, S-ADMIRAL-MOD), clamped to [0, 1]; a jammed
    /// rudder (S-CRIT-PART) lowers it.
    /// </summary>
    public static double RetreatDecisionChance(Ship ship, int tick, GameDefines defines, double enemyRetreatChance = 0)
    {
        var navy = defines.NNavy;
        var chance = navy.COMBAT_RETREAT_DECISION_CHANCE * (1 + MemberModifierRules.ShipModifier(ship, StaticModifiers.NavalRetreatChance, 0, defines));

        if (tick > navy.COMBAT_INITIAL_DURATION)
        {
            chance *= 1 + MemberModifierRules.ShipModifier(ship, StaticModifiers.NavalRetreatChanceAfterInitialCombat, 0, defines);
        }

        chance *= 1 + enemyRetreatChance;
        return Math.Clamp(chance, 0, 1);
    }

    /// <summary>
    /// [RE] 0x196fe80, per day: BASE_ESCAPE_SPEED + min(tick * ESCAPE_SPEED_PER_COMBAT_DAY / 24, MAX) + retreat speed
    /// modifiers + speed * SPEED_TO_ESCAPE_SPEED / 100, plus the submarine bonus (ESCAPE_SPEED_HIDDEN_SUB while hidden).
    /// The retreat speed modifier is naval_retreat_speed for warships (plus naval_retreat_speed_after_initial_combat after
    /// COMBAT_INITIAL_DURATION), sub_retreat_speed for submarines and convoy_retreat_speed for convoys.
    /// </summary>
    public static double EscapeSpeedPerDay(Ship ship, int tick, ScreeningSummary ownScreening, double darkness, GameDefines defines)
    {
        var navy = defines.NNavy;
        var speed = navy.BASE_ESCAPE_SPEED + Math.Min(tick * navy.ESCAPE_SPEED_PER_COMBAT_DAY / 24.0, navy.MAX_ESCAPE_SPEED_FROM_COMBAT_DURATION);
        var retreatSpeedKey = ship.Role switch
        {
            ShipRole.Convoy => StaticModifiers.ConvoyRetreatSpeed,
            ShipRole.Submarine => StaticModifiers.SubRetreatSpeed,
            _ => StaticModifiers.NavalRetreatSpeed
        };

        speed += MemberModifierRules.Get(ship, retreatSpeedKey, ownScreening, darkness, defines);

        if (retreatSpeedKey == StaticModifiers.NavalRetreatSpeed && tick > navy.COMBAT_INITIAL_DURATION)
        {
            speed += MemberModifierRules.Get(ship, StaticModifiers.NavalRetreatSpeedAfterInitialCombat, ownScreening, darkness, defines);
        }

        speed += CombatStatRules.CombatSpeed(ship, defines) * navy.SPEED_TO_ESCAPE_SPEED / 100.0;

        if (ship.Role == ShipRole.Submarine)
        {
            speed += ship.IsVisible ? navy.ESCAPE_SPEED_SUB_BASE : navy.ESCAPE_SPEED_HIDDEN_SUB;
        }

        return Math.Max(0, speed);
    }

    /// <summary>[RE] Retreating ships add the daily escape speed / 24 each hour and escape at 1.</summary>
    public static bool AdvanceRetreat(Ship ship, int tick, ScreeningSummary ownScreening, double darkness, GameDefines defines)
    {
        if (ship.CurrentStatus != ShipStatus.Retreating || ship.IsSunk)
        {
            return false;
        }

        ship.RetreatProgress += EscapeSpeedPerDay(ship, tick, ownScreening, darkness, defines) / 24.0;

        if (ship.RetreatProgress < 1.0)
        {
            return false;
        }

        ship.CurrentStatus = ShipStatus.Retreated;
        return true;
    }
}
