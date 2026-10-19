using NavySimulator.Engine.Defines;

namespace NavySimulator.Engine.Battles.Mechanics;

/// <summary>
/// S-STATS-COMBAT [RE]: the ship values combat reads through the ship modifier sum (country modifiers, damaged parts).
/// Spec: docs/mechanics/surface.md.
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

    // [RE] 0xc3d4a3: max(0.001, (max_organisation + navy_org) × (1 + navy_org_factor)); a training mission's
    // TRAINING_ORG does not apply in battle.
    public static double MaxOrganization(Ship ship, GameDefines defines)
    {
        var flat = MemberModifierRules.ShipModifier(ship, StaticModifiers.NavyOrg, 0, defines);
        var factor = MemberModifierRules.ShipModifier(ship, StaticModifiers.NavyOrgFactor, 0, defines);
        return Math.Max(defines.Hardcoded.MinMaxOrganization, (ship.GetFinalStats().Organization + flat) * (1 + factor));
    }
}
