using NavySimulator.Engine.Defines;
using NavySimulator.Engine.Environment;

namespace NavySimulator.Engine.Battles.Mechanics;

/// <summary>
/// [RE] The member modifier getter 0x1972570, which the combat rules read every modifier through (S-HIT-MODS,
/// S-RETREAT-SPEED). Spec: docs/mechanics/surface.md.
/// </summary>
public static class MemberModifierRules
{
    /// <summary>
    /// One modifier of a ship in battle: the ship's modifier sum (<see cref="ShipModifier"/>), plus screening_bonus ×
    /// screening for the battle line and carrier line, and capital_screening_bonus × combined carrier screening for the
    /// carrier line. Every key of the two blocks applies, the per-weapon hit chance factors included (S-SCREEN [RE + CAL]:
    /// TFB's screening_bonus).
    /// </summary>
    public static double Get(Ship ship, string key, ScreeningSummary ownScreening, BattleEnvironment environment, GameDefines defines)
    {
        var modifiers = defines.StaticModifiers;
        var value = ShipModifier(ship, key, environment, defines);
        var line = TargetingRules.LineOf(ship);

        if (line is 2 or 3)
        {
            value += ownScreening.ScreeningEfficiency * modifiers.Get(StaticModifiers.ScreeningBonus, key);
        }

        if (line == 3)
        {
            value += ScreeningRules.CombinedCarrierScreening(ownScreening) * modifiers.Get(StaticModifiers.CapitalScreeningBonus, key);
        }

        return value;
    }

    /// <summary>
    /// [RE] The ship modifier sum 0xc38760, which the ship's own getters (combat speed, max organization, organization
    /// regain, retreat chance) read: country modifiers, the static pride_of_the_fleet block on the pride of the fleet itself
    /// (S-POTF, 0xc387e2), the night modifier × darkness, the naval terrain's block for the ship's sub-unit (S-TERRAIN), the
    /// region's weather × the ship's weather factor (S-WEATHER), the fleet admiral's modifiers
    /// (S-ADMIRAL-MOD: container A and the traits' sub-unit modifiers for the ship), and each damaged part's modifier ×
    /// its count × (1 + naval_critical_effect_factor) (S-CRIT-PART; [CAL] a broken propeller under TFB's damage control
    /// left 0.28 of a ship's speed). [RE] 0xc38b58: that factor is a full, separate read of this sum, admiral included.
    /// </summary>
    public static double ShipModifier(Ship ship, string key, BattleEnvironment environment, GameDefines defines)
    {
        var value = ship.CountryModifiers.Get(key) + environment.Darkness * defines.StaticModifiers.Get(StaticModifiers.Night, key) +
                    ship.AdmiralModifiers.Get(key);

        // S-TERRAIN [RE] 0xc388c2 (0x143d340): the naval terrain's block for the ship's sub-unit, which the game merges with
        // the terrain's own keys at load (0x143e980): the terrain's keys plus the sub-unit's.
        if (environment.Terrain is { } terrain)
        {
            value += terrain.Modifiers.Get(key) + terrain.SubUnitModifier(ship.Design.ShipType.ID).Get(key);
        }

        // S-WEATHER [RE] 0xc389cb: the region's weather holder × the ship's weather factor.
        if (environment.WeatherModifiers is { } weather)
        {
            var weatherValue = weather.Get(key);

            if (weatherValue != 0)
            {
                value += weatherValue * WeatherFactor(ship);
            }
        }

        if (ship.IsPrideOfTheFleet)
        {
            value += defines.StaticModifiers.Get(StaticModifiers.PrideOfTheFleet, key);
        }
        var parts = ship.PartModifier(key);

        if (parts != 0)
        {
            var effect = key == StaticModifiers.NavalCriticalEffectFactor ? 0 : ShipModifier(ship, StaticModifiers.NavalCriticalEffectFactor, environment, defines);
            value += parts * (1 + effect);
        }

        return value;
    }

    /// <summary>
    /// S-WEATHER [RE] 0xc389cb, 0xd67310: how much of the region's weather a ship takes:
    /// <c>naval_weather_penalty_factor − 1 + clamp(1 + navy_weather_penalty, 0, 1)</c>. The hull archetypes give the stat 1,
    /// so a normal ship takes the weather whole; a hull without it (TFB's super-heavy) takes none.
    /// </summary>
    public static double WeatherFactor(Ship ship)
    {
        return ship.GetFinalStats().NavalWeatherPenaltyFactor - 1 +
               Math.Clamp(1 + ship.CountryModifiers.Get(StaticModifiers.NavyWeatherPenalty), 0, 1);
    }
}
