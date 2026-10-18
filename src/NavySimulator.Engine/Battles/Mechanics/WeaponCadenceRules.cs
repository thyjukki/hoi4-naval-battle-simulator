using NavySimulator.Engine.Defines;

namespace NavySimulator.Engine.Battles.Mechanics;

/// <summary>
/// S-ACTIVATE and S-COOLDOWN: when each line starts firing and how often each weapon fires. Spec: docs/mechanics/surface.md.
/// </summary>
public static class WeaponCadenceRules
{
    public static int ActivationHour(ShipRole role, bool battleHasCarriers, bool battleHasCapitals, GameDefines defines, BattleRules rules)
    {
        var navy = defines.NNavy;

        if (!rules.ActivationDependsOnComposition)
        {
            return (int)(role switch
            {
                ShipRole.Carrier => navy.CARRIER_ONLY_COMBAT_ACTIVATE_TIME,
                ShipRole.Capital => navy.CAPITAL_ONLY_COMBAT_ACTIVATE_TIME,
                _ => navy.ALL_SHIPS_ACTIVATE_TIME
            });
        }

        return (int)(role switch
        {
            ShipRole.Carrier => navy.CARRIER_ONLY_COMBAT_ACTIVATE_TIME,
            ShipRole.Capital => battleHasCarriers ? navy.CAPITAL_ONLY_COMBAT_ACTIVATE_TIME : 0,
            _ => battleHasCarriers ? navy.ALL_SHIPS_ACTIVATE_TIME : battleHasCapitals ? navy.CAPITAL_ONLY_COMBAT_ACTIVATE_TIME : 0
        });
    }

    // [DEFINE] BASE_GUN_COOLDOWNS is ordered { heavy, torpedo, light } (same order as GUN_HIT_PROFILES).
    // Depth charges use the light cooldown [ASSUMED].
    public static double CooldownHours(WeaponType weapon, GameDefines defines)
    {
        var navy = defines.NNavy;
        var cooldown = weapon switch
        {
            WeaponType.Heavy => navy.BASE_GUN_COOLDOWNS[0],
            WeaponType.Torpedo => navy.BASE_GUN_COOLDOWNS[1],
            _ => navy.BASE_GUN_COOLDOWNS[2]
        };

        return Math.Max(navy.MIN_GUN_COOLDOWN, cooldown);
    }

    /// <summary>
    /// Returns null if the weapon fires this hour (and schedules its next shot), otherwise the skip reason.
    /// </summary>
    public static string? TryFire(Ship ship, WeaponType weapon, int hour, int activationHour, GameDefines defines, BattleRules rules)
    {
        if (hour < activationHour)
        {
            return SkipReasons.LineNotActive;
        }

        var cooldown = CooldownHours(weapon, defines);

        if (!ship.NextFireHour.TryGetValue(weapon, out var nextFireHour))
        {
            nextFireHour = activationHour + (rules.FirstShotWaitsForCooldown ? cooldown : 0);
        }

        if (hour + 1e-9 < nextFireHour)
        {
            ship.NextFireHour[weapon] = nextFireHour;
            return SkipReasons.Cooldown;
        }

        ship.NextFireHour[weapon] = Math.Max(nextFireHour, hour) + cooldown;
        return null;
    }
}
