using NavySimulator.Engine.Defines;

namespace NavySimulator.Engine.Battles.Mechanics;

/// <summary>
/// S-STATS-COMBAT [RE]: the ship values combat reads through the ship modifier sum (country modifiers, damaged parts):
/// speed, organization, attack and armor, AA, visibility. Spec: docs/mechanics/surface.md.
/// </summary>
public static class CombatStatRules
{
    // [RE] 0xc3a740: speed × max(0.1, 1 + naval_speed_factor); admiral and fuel terms are not modelled. Hit profile
    // (S-HIT-SHIP) and escape speed (S-RETREAT-SPEED) read it.
    public static double CombatSpeed(Ship ship, GameDefines defines)
    {
        var factor = MemberModifierRules.ShipModifier(ship, StaticModifiers.NavalSpeedFactor, 0, defines);
        return ship.GetFinalStats().Speed * Math.Max(defines.Hardcoded.MinCombatSpeedFactor, 1 + factor);
    }

    /// <summary>
    /// S-STATS-COMBAT [RE] 0xc34a50: the combat attack (or, with <paramref name="isArmor"/>, armor) of a ship stat:
    /// <c>max(0, (stat [+ submarine_attack for submarines' attacks]) × (1 + Σ))</c>, 0 when the stat (with that add) is 0.
    /// Σ is read from the ship modifier sum: by the ship type's sub-unit <c>type</c>, navy_submarine / capital_ship /
    /// screen_attack (defence) factor (carriers have none of the three); for attacks naval_night_attack × darkness;
    /// naval_damage_factor (defense_factor), country, admiral and experience level; for attacks, the commanding admiral's
    /// adjusters (S-ADMIRAL-ADJUST): its traits' <c>units</c> adjuster for the ship's sub-unit and night adjuster ×
    /// darkness. The target's armor is read without them (the game passes no admiral). While the side's pride of the fleet
    /// is afloat, the commanding admiral's naval_has_potf_in_combat_attack (armor: _defense) joins (S-POTF). Naval
    /// terrain (and the admiral's terrain adjusters), fuel and <c>_against</c> terms are not modelled.
    /// </summary>
    public static double CombatValue(Ship ship, double stat, bool isArmor, double darkness, GameDefines defines)
    {
        double Mod(string key) => MemberModifierRules.ShipModifier(ship, key, darkness, defines);
        var types = ship.Design.ShipType.UnitTypes;
        var sum = 0.0;

        if (types.Contains(SubmarineType))
        {
            if (!isArmor)
            {
                stat += Mod(StaticModifiers.SubmarineAttack);
            }

            sum += Mod(isArmor ? StaticModifiers.NavySubmarineDefenceFactor : StaticModifiers.NavySubmarineAttackFactor);
        }

        if (types.Contains(CapitalShipType))
        {
            sum += Mod(isArmor ? StaticModifiers.NavyCapitalShipDefenceFactor : StaticModifiers.NavyCapitalShipAttackFactor);
        }

        if (types.Contains(ScreenShipType))
        {
            sum += Mod(isArmor ? StaticModifiers.NavyScreenDefenceFactor : StaticModifiers.NavyScreenAttackFactor);
        }

        if (stat == 0)
        {
            return 0;
        }

        if (!isArmor)
        {
            sum += Mod(StaticModifiers.NavalNightAttack) * darkness;
        }

        sum += isArmor
            ? ExperienceRules.NavalDefenseFactor(ship, defines) + Mod(StaticModifiers.NavalDefenseFactor)
            : ExperienceRules.NavalDamageFactor(ship, defines) + Mod(StaticModifiers.NavalDamageFactor);

        // S-ADMIRAL-ADJUST [RE] 0xc34a50: the shot profile passes the commanding admiral for attacks only.
        if (!isArmor && ship.Admiral is { } admiral)
        {
            sum += admiral.Adjuster(Leaders.LeaderTrait.Night).Attack * darkness + admiral.SubUnitAdjuster(ship.Design.ShipType.ID).Attack;
        }

        // S-POTF [RE] 0xc351ae: read from the commanding admiral of the ship's side for attacks and armor alike.
        if (ship.PrideOfTheFleet is { } pride && PrideOfTheFleetRules.IsInBattle(pride) && ship.Admiral is { } commander)
        {
            sum += commander.CombatModifiers.Get(isArmor ? StaticModifiers.NavalHasPotfInCombatDefense : StaticModifiers.NavalHasPotfInCombatAttack);
        }

        return Math.Max(0, stat * (1 + sum));
    }

    /// <summary>
    /// [RE] 0xc3a470: <c>(anti_air_attack + navy_anti_air_attack) × (1 + navy_anti_air_attack_factor)</c>, no clamp; the AA
    /// the fleet AA sum, preemptive AA and air target weights read.
    /// </summary>
    public static double AntiAir(Ship ship, GameDefines defines)
    {
        var flat = MemberModifierRules.ShipModifier(ship, StaticModifiers.NavyAntiAirAttack, 0, defines);
        var factor = MemberModifierRules.ShipModifier(ship, StaticModifiers.NavyAntiAirAttackFactor, 0, defines);
        return (ship.GetFinalStats().AntiAir + flat) * (1 + factor);
    }

    /// <summary>
    /// [RE] 0xc3aa20 / 0xc3ab80: sub or surface visibility <c>max(0, stat × (1 + navy_visibility))</c>, which the hit
    /// profile (S-HIT-SHIP) and the submarine reveal (S-SUB) read.
    /// </summary>
    public static double Visibility(Ship ship, bool submerged, GameDefines defines)
    {
        var stats = ship.GetFinalStats();
        var factor = 1 + MemberModifierRules.ShipModifier(ship, StaticModifiers.NavyVisibility, 0, defines);
        return Math.Max(0, (submerged ? stats.SubVisibility : stats.SurfaceVisibility) * factor);
    }

    // The sub-unit type keywords (common/units type = { ... }) that gate the combat getter's factors.
    private const string SubmarineType = "submarine";
    private const string CapitalShipType = "capital_ship";
    private const string ScreenShipType = "screen_ship";

    // [RE] 0xc3d4a3: max(0.001, (max_organisation + navy_org) × (1 + navy_org_factor)); a training mission's
    // TRAINING_ORG does not apply in battle.
    public static double MaxOrganization(Ship ship, GameDefines defines)
    {
        var flat = MemberModifierRules.ShipModifier(ship, StaticModifiers.NavyOrg, 0, defines);
        var factor = MemberModifierRules.ShipModifier(ship, StaticModifiers.NavyOrgFactor, 0, defines);
        return Math.Max(defines.Hardcoded.MinMaxOrganization, (ship.GetFinalStats().Organization + flat) * (1 + factor));
    }
}
