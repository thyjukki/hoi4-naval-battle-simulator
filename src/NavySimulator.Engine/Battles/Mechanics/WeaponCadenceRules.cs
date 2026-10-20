using NavySimulator.Engine.Defines;
using NavySimulator.Engine.Environment;

namespace NavySimulator.Engine.Battles.Mechanics;

/// <summary>
/// S-ACTIVATE and S-COOLDOWN [RE]: when a ship's line activates and how many shots a weapon slot fires per hour.
/// Spec: docs/mechanics/surface.md. "Tick" is the game's combat hour counter, which is 1 during the first hour.
/// </summary>
public static class WeaponCadenceRules
{
    /// <summary>
    /// [RE] 0x1971610: activation threshold for a ship. Composition is pooled over both sides and counts only visible
    /// ships still in the battle: carrier-like = carriers, capital-like = capitals and submarines.
    /// </summary>
    public static int ActivationThreshold(Ship ship, IEnumerable<Ship> allShipsInBattle, GameDefines defines)
    {
        var navy = defines.NNavy;
        var carrierTime = (int)navy.CARRIER_ONLY_COMBAT_ACTIVATE_TIME;
        var capitalTime = (int)navy.CAPITAL_ONLY_COMBAT_ACTIVATE_TIME;
        var allTime = (int)navy.ALL_SHIPS_ACTIVATE_TIME;

        var visible = allShipsInBattle.Where(other => other.IsInBattle && other.IsVisible).ToList();
        var hasCarriers = visible.Any(other => other.Role == ShipRole.Carrier);
        var hasCapitals = visible.Any(other => other.Role is ShipRole.Capital or ShipRole.Submarine);

        if (!hasCarriers)
        {
            allTime = allTime + carrierTime - capitalTime;
            capitalTime = carrierTime;

            if (!hasCapitals)
            {
                allTime = carrierTime;
            }
        }

        return ship.Role switch
        {
            ShipRole.Convoy => allTime,
            ShipRole.Carrier => carrierTime,
            ShipRole.Capital or ShipRole.Submarine => capitalTime,
            _ => allTime
        };
    }

    /// <summary>[RE] A waiting ship becomes active at the first tick strictly above its threshold; it fires from the next tick.</summary>
    public static bool IsActivated(Ship ship, int tick, IEnumerable<Ship> allShipsInBattle, GameDefines defines)
    {
        return tick > (int)defines.NNavy.ALL_SHIPS_ACTIVATE_TIME || tick > ActivationThreshold(ship, allShipsInBattle, defines);
    }

    // [RE] 0x1972a70: BASE_GUN_COOLDOWNS is indexed by slot { heavy, torpedo, light }; a ship's torpedo cooldown is
    // x (1 + naval_torpedo_cooldown_factor) (modifier 0x1be, ship modifier sum); at least MIN_GUN_COOLDOWN.
    public static double CooldownHours(WeaponSlot slot, GameDefines defines, Ship? ship = null)
    {
        var navy = defines.NNavy;
        var cooldown = navy.BASE_GUN_COOLDOWNS[(int)slot];

        if (slot == WeaponSlot.Torpedo && ship is not null)
        {
            cooldown *= 1 + MemberModifierRules.ShipModifier(ship, StaticModifiers.NavalTorpedoCooldownFactor, BattleEnvironment.None, defines);
        }

        return Math.Max(navy.MIN_GUN_COOLDOWN, cooldown);
    }

    /// <summary>
    /// [RE] 0x1973e20: called once per hour for a slot that has attack. Adds one hour of charge (capped at one rounded-up
    /// cooldown) and returns true if the slot has enough charge to fire.
    /// </summary>
    public static bool Charge(Ship ship, WeaponSlot slot, GameDefines defines)
    {
        var cooldown = CooldownHours(slot, defines, ship);
        var index = (int)slot;
        ship.SlotCharge[index] = Math.Min(ship.SlotCharge[index] + 1.0, Math.Ceiling(cooldown));
        return ship.SlotCharge[index] >= cooldown;
    }

    /// <summary>[RE] 0x19740c0: spends one cooldown of charge for a shot; returns false when the slot is out of charge.</summary>
    public static bool TrySpendShot(Ship ship, WeaponSlot slot, GameDefines defines)
    {
        var cooldown = CooldownHours(slot, defines, ship);
        var index = (int)slot;

        if (ship.SlotCharge[index] < cooldown)
        {
            return false;
        }

        ship.SlotCharge[index] -= cooldown;
        return true;
    }
}
