using NavySimulator.Engine.Defines;

namespace NavySimulator.Engine.Battles.Mechanics;

/// <summary>
/// [RE] The member modifier getter 0x1972570, which the combat rules read every modifier through (S-HIT-MODS,
/// S-RETREAT-SPEED). Spec: docs/mechanics/surface.md.
/// </summary>
public static class MemberModifierRules
{
    /// <summary>
    /// One modifier of a ship in battle: the ship's modifier sum 0xc38760 (country modifiers, S-STATS-COUNTRY [CAL]; the
    /// night static modifier × darkness), plus screening_bonus × screening for the battle line and carrier line, and
    /// capital_screening_bonus × combined carrier screening for the carrier line. Every key of the two blocks applies,
    /// the per-weapon hit chance factors included (S-SCREEN [RE + CAL]: TFB's screening_bonus).
    /// </summary>
    public static double Get(Ship ship, string key, ScreeningSummary ownScreening, double darkness, GameDefines defines)
    {
        var modifiers = defines.StaticModifiers;
        var value = ship.CountryModifiers.Get(key) + darkness * modifiers.Get(StaticModifiers.Night, key);
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
}
