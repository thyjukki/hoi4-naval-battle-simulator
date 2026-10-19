using NavySimulator.Engine.Defines;

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
    public static double Get(Ship ship, string key, ScreeningSummary ownScreening, double darkness, GameDefines defines)
    {
        var modifiers = defines.StaticModifiers;
        var value = ShipModifier(ship, key, darkness, defines);
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
    /// regain, retreat chance) read: country modifiers, the night modifier × darkness, and each damaged part's modifier ×
    /// its count × (1 + naval_critical_effect_factor) (S-CRIT-PART; [CAL] a broken propeller under TFB's damage control
    /// left 0.28 of a ship's speed).
    /// </summary>
    public static double ShipModifier(Ship ship, string key, double darkness, GameDefines defines)
    {
        var value = ship.CountryModifiers.Get(key) + darkness * defines.StaticModifiers.Get(StaticModifiers.Night, key);
        var parts = ship.PartModifier(key);

        if (parts != 0)
        {
            value += parts * (1 + ship.CountryModifiers.Get(StaticModifiers.NavalCriticalEffectFactor));
        }

        return value;
    }
}
