using NavySimulator.Engine.Defines;
using NavySimulator.Engine.Random;
using NavySimulator.Engine.Stats;

namespace NavySimulator.Engine.Battles.Mechanics;

/// <summary>
/// S-RECOVERY [RE + LOG] (0xd77ff0, the task force's remove-sunk-ship function, called by the combat's sink handler
/// 0x1975480): a ship that sinks may be recovered instead. Spec: docs/mechanics/surface.md; finding
/// S-RECOVERY-ship-recovery-chance.md.
/// </summary>
public static class RecoveryRules
{
    /// <summary>
    /// The chance before the factor: the task force's support entries (0xd6fa50: each support type's static modifier ×
    /// its support ratio, e.g. naval_repair_support's 0.1; not modelled, the simulator has no support ships) plus the owner
    /// country's naval_ship_recovery_chance (0x27f). NAVAL_DOMINANCE_SHIP_RECOVERY_CHANCE is added only for a task force
    /// whose province has a controller (a port, 0xe796b0), so never in a battle at sea [LOG].
    /// </summary>
    public static double Chance(ModifierSet country)
    {
        return country.Get(StaticModifiers.NavalShipRecoveryChance);
    }

    /// <summary>
    /// [RE + LOG] 0xd7810d: the chance times the owner's naval_ship_recovery_chance_factor (0x280) itself, not 1 + it. No
    /// game or TFB script sets that factor, so the final chance is 0 for every country (the probe battles read 0 at all
    /// 71 sinkings) and no ship is ever recovered unless a mod gives the factor.
    /// </summary>
    public static double FinalChance(double chance, ModifierSet country)
    {
        return FixedPoint.Truncate(chance * country.Get(StaticModifiers.NavalShipRecoveryChanceFactor));
    }

    /// <summary>
    /// [RE + LOG] 0xd781e4: the recovered ship's HP is SUPPORT_SHIP_RECOVERY_BASE_STRENGTH_FACTOR × the summed maximum HP
    /// of the ships still in its task force (task force vt+0x108; the ship included, earlier losses not), at most its own
    /// maximum. Logged: 18 of 18 forced recoveries (a 132 HP light cruiser of a 41-ship task force came back at 120.52).
    /// The simulator takes a side as one task force.
    /// </summary>
    public static double RecoveredHp(Ship ship, IEnumerable<Ship> taskForce, GameDefines defines)
    {
        var total = taskForce.Where(other => !other.IsSunk || ReferenceEquals(other, ship)).Sum(other => other.GetFinalStats().Hp);
        return Math.Min(ship.GetFinalStats().Hp, FixedPoint.Truncate(total * defines.NNavy.SUPPORT_SHIP_RECOVERY_BASE_STRENGTH_FACTOR));
    }

    /// <summary>
    /// [RE + LOG] 0xd780ed..0xd781c4: when the chance before the factor is above 0 the game draws the roll (taskforce.cpp
    /// line 0xb31, an integer draw mixed into 0..99999) and recovers the ship if it is below the final chance. A recovered
    /// ship leaves the battle like a sunk one (its member state is sunk) with the HP of <see cref="RecoveredHp"/>, and the
    /// task force is not refilled. Returns whether <paramref name="ship"/>, just sunk, was recovered.
    /// </summary>
    public static bool TryRecover(Ship ship, BattleSide side, GameDefines defines, IRandomSource random)
    {
        var country = side.Participant.CountryModifiers;
        var chance = Chance(country);

        if (chance <= 0)
        {
            return false;
        }

        var roll = random.NextDouble(RollKind.ShipRecovery);

        if (FixedPoint.Raw(roll) >= FixedPoint.Raw(FinalChance(chance, country)))
        {
            return false;
        }

        ship.CurrentHP = RecoveredHp(ship, side.Ships, defines);
        ship.IsRecovered = true;
        return true;
    }
}
