using NavySimulator.Engine.Defines;
using NavySimulator.Engine.Environment;

namespace NavySimulator.Engine.Battles.Mechanics;

/// <summary>
/// S-STATS-COMBAT [RE]: the ship values combat reads through the ship modifier sum (country modifiers, damaged parts):
/// speed, organization, attack and armor, AA, visibility. Spec: docs/mechanics/surface.md.
/// </summary>
public static class CombatStatRules
{
    // [RE] 0xc3a740: speed × max(0.1, 1 + naval_speed_factor + the naval terrain's movement adjuster for the ship's
    // sub-unit (S-TERRAIN, 0x143e4a0 +0x20)); fuel is not modelled. Hit profile (S-HIT-SHIP) and escape speed
    // (S-RETREAT-SPEED) read it.
    public static double CombatSpeed(Ship ship, GameDefines defines, BattleEnvironment environment = default)
    {
        var factor = MemberModifierRules.ShipModifier(ship, StaticModifiers.NavalSpeedFactor, environment, defines);

        if (environment.Terrain is { } terrain)
        {
            factor += terrain.SubUnitAdjuster(ship.Design.ShipType.ID).Movement;
        }

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
    /// is afloat, the commanding admiral's naval_has_potf_in_combat_attack (armor: _defense) joins (S-POTF). The battle's
    /// naval terrain adds its units adjuster for the ship's sub-unit, and for attacks the admiral's adjuster for that
    /// terrain (S-TERRAIN). Fuel and <c>_against</c> terms are not modelled.
    /// </summary>
    public static double CombatValue(Ship ship, double stat, bool isArmor, BattleEnvironment environment, GameDefines defines)
    {
        double Mod(string key) => MemberModifierRules.ShipModifier(ship, key, environment, defines);
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
            sum += Mod(StaticModifiers.NavalNightAttack) * environment.Darkness;
        }

        sum += isArmor
            ? ExperienceRules.NavalDefenseFactor(ship, defines) + Mod(StaticModifiers.NavalDefenseFactor)
            : ExperienceRules.NavalDamageFactor(ship, defines) + Mod(StaticModifiers.NavalDamageFactor);

        // S-TERRAIN [RE] 0xc34a50 (0x143e4a0): the region's naval terrain's units adjuster for the ship's sub-unit, with or
        // without an admiral, so the target's armor gets it too.
        if (environment.Terrain is { } terrain)
        {
            var adjuster = terrain.SubUnitAdjuster(ship.Design.ShipType.ID);
            sum += isArmor ? adjuster.Defence : adjuster.Attack;
        }

        // S-ADMIRAL-ADJUST [RE] 0xc34a50: the shot profile passes the commanding admiral for attacks only: the traits'
        // adjusters for the battle's naval terrain (0xc19c40) and night × darkness, and the sub-unit adjuster.
        if (!isArmor && ship.Admiral is { } admiral)
        {
            sum += (environment.Terrain is { } battleTerrain ? admiral.Adjuster(battleTerrain.ID).Attack : 0) +
                   admiral.Adjuster(Leaders.LeaderTrait.Night).Attack * environment.Darkness + admiral.SubUnitAdjuster(ship.Design.ShipType.ID).Attack;
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
    public static double AntiAir(Ship ship, GameDefines defines, BattleEnvironment environment = default)
    {
        var flat = MemberModifierRules.ShipModifier(ship, StaticModifiers.NavyAntiAirAttack, environment, defines);
        var factor = MemberModifierRules.ShipModifier(ship, StaticModifiers.NavyAntiAirAttackFactor, environment, defines);
        return (ship.GetFinalStats().AntiAir + flat) * (1 + factor);
    }

    /// <summary>
    /// [RE] 0xc3aa20 / 0xc3ab80: sub or surface visibility <c>max(0, stat × (1 + navy_visibility))</c>, which the hit
    /// profile (S-HIT-SHIP) and the submarine reveal (S-SUB) read.
    /// </summary>
    public static double Visibility(Ship ship, bool submerged, GameDefines defines, BattleEnvironment environment = default)
    {
        var stats = ship.GetFinalStats();
        var factor = 1 + MemberModifierRules.ShipModifier(ship, StaticModifiers.NavyVisibility, environment, defines);
        return Math.Max(0, (submerged ? stats.SubVisibility : stats.SurfaceVisibility) * factor);
    }

    /// <summary>
    /// [RE + CAL] 0xc3aae0: effective surface detection <c>max(0, stat × (1 + naval_detection))</c>, through the ship
    /// modifier sum (so the region's weather × the ship's weather factor, S-WEATHER); S-POS-DETECT sums it. The carrier
    /// part (carrier_surface_detection × deck fill, medium confidence) is not added.
    /// </summary>
    public static double SurfaceDetection(Ship ship, GameDefines defines, BattleEnvironment environment = default)
    {
        var factor = 1 + MemberModifierRules.ShipModifier(ship, StaticModifiers.NavalDetection, environment, defines);
        return Math.Max(0, ship.GetFinalStats().SurfaceDetection * factor);
    }

    // The sub-unit type keywords (common/units type = { ... }) that gate the combat getter's factors.
    private const string SubmarineType = "submarine";
    private const string CapitalShipType = "capital_ship";
    private const string ScreenShipType = "screen_ship";

    // [RE] 0xc3d4a3: max(0.001, (max_organisation + navy_org) × (1 + navy_org_factor)); a training mission's
    // TRAINING_ORG does not apply in battle.
    public static double MaxOrganization(Ship ship, GameDefines defines, BattleEnvironment environment = default)
    {
        var flat = MemberModifierRules.ShipModifier(ship, StaticModifiers.NavyOrg, environment, defines);
        var factor = MemberModifierRules.ShipModifier(ship, StaticModifiers.NavyOrgFactor, environment, defines);
        return Math.Max(defines.Hardcoded.MinMaxOrganization, (ship.GetFinalStats().Organization + flat) * (1 + factor));
    }
}
