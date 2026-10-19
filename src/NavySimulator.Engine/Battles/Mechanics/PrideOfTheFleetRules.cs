namespace NavySimulator.Engine.Battles.Mechanics;

/// <summary>
/// S-POTF: the pride of the fleet, one ship a country marks (country+0x250). It carries the static pride_of_the_fleet
/// block (<see cref="MemberModifierRules.ShipModifier"/>, <see cref="ExperienceRules.ModifiedGain"/>), and while it is
/// afloat every ship of its side gets the commanding admiral's naval_has_potf_in_combat_attack / _defense
/// (<see cref="CombatStatRules.CombatValue"/>). Spec: docs/mechanics/surface.md; finding S-POTF-pride-of-the-fleet.md.
/// </summary>
public static class PrideOfTheFleetRules
{
    // The sub-unit and equipment type keywords 0xc33a50 accepts (capital_ship on the sub-unit only).
    private const string CapitalShipType = "capital_ship";
    private const string CarrierType = "carrier";
    private const string MissileLauncherType = "missile_launcher";

    /// <summary>
    /// S-POTF [RE] 0xc34a50: the pride of the fleet's task force is in this combat. It stays there in every member state
    /// (waiting, retreating, escaped) until the battle ends; only sinking deletes the ship and clears the country's handle.
    /// </summary>
    public static bool IsInBattle(Ship pride)
    {
        return !pride.IsSunk;
    }

    /// <summary>
    /// S-POTF [RE] 0xc33a50: the ships the game lets a country assign: a sub-unit of type capital_ship, carrier or
    /// missile_launcher, or a design whose equipment has type carrier or missile_launcher. Vanilla: battleship,
    /// battlecruiser, heavy cruiser, carrier; TFB adds the super heavy battleship and carrier.
    /// </summary>
    public static bool CanBePrideOfTheFleet(ShipDesign design)
    {
        var unitTypes = design.ShipType.UnitTypes;
        return unitTypes.Contains(CapitalShipType) || unitTypes.Contains(CarrierType) || unitTypes.Contains(MissileLauncherType) ||
               design.Types.Contains(CarrierType) || design.Types.Contains(MissileLauncherType);
    }
}
