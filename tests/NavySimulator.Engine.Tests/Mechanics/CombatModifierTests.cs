using NavySimulator.Engine.Battles;
using NavySimulator.Engine.Battles.Mechanics;
using NavySimulator.Engine.Defines;
using NavySimulator.Engine.Stats;
using NavySimulator.Engine.Tests.TestSupport;

namespace NavySimulator.Engine.Tests.Mechanics;

/// <summary>S-STATS-COMBAT [RE]: the country modifier terms of the combat getters (0xc34a50, 0xc3a470, 0xc3aa20, 0x1972a70, 0xc3e305).</summary>
public class CombatModifierTests
{
    private readonly GameDefines defines = GameDefines.Vanilla();

    [Theory]
    [InlineData("capital_ship", 0.1, 0.2)]
    [InlineData("screen_ship", 0.3, 0.4)]
    [InlineData("submarine", 0.5, 0.6)]
    [InlineData("carrier", 0, 0)]
    public void AttackAndArmor_TakeTheFactorsOfTheSubUnitType(string unitType, double attackFactor, double defenceFactor)
    {
        var ship = Ship(unitType, Modifiers());

        Assert.Equal(40 * (1 + attackFactor), CombatStatRules.CombatValue(ship, 40, false, 0, defines), 9);
        Assert.Equal(50 * (1 + defenceFactor), CombatStatRules.CombatValue(ship, 50, true, 0, defines), 9);
    }

    [Fact]
    public void SubmarineAttack_IsAFlatAddToTheAttack_AndNightAttackScalesWithDarkness()
    {
        var submarine = Ship("submarine", new ModifierSet([new("submarine_attack", 2), new("naval_night_attack", 0.1)]));
        var screen = Ship("screen_ship", new ModifierSet([new("submarine_attack", 2), new("naval_night_attack", 0.1)]));

        Assert.Equal((20 + 2) * 1.05, CombatStatRules.CombatValue(submarine, 20, false, 0.5, defines), 9);
        Assert.Equal(20 * 1.05, CombatStatRules.CombatValue(screen, 20, false, 0.5, defines), 9);
        Assert.Equal(10, CombatStatRules.CombatValue(screen, 10, true, 1, defines), 9);
    }

    [Fact]
    public void CountryDamageFactor_CountsInTheAttackAndAgainInTheHitMultiplier()
    {
        var shooter = Ship("screen_ship", new ModifierSet([new("naval_damage_factor", 0.1)]));
        var target = Ship("screen_ship", ModifierSet.Empty);

        var profile = DamageRules.Profile(shooter, target, WeaponSlot.Light, 1, defines);

        // Light attack 5 against armor 50 is the lowest piercing tier.
        var piercing = defines.NNavy.NAVY_PIERCING_THRESHOLD_DAMAGE_VALUES[profile.PiercingTier];
        Assert.Equal(5 * 1.1 * piercing * 1.1, profile.BaseDamage, 9);
    }

    [Fact]
    public void AntiAir_AddsTheFlatModifier_ThenScales()
    {
        var ship = Ship("capital_ship", new ModifierSet([new("navy_anti_air_attack", 2), new("navy_anti_air_attack_factor", 0.15)]));

        Assert.Equal((ship.GetFinalStats().AntiAir + 2) * 1.15, CombatStatRules.AntiAir(ship, defines), 9);
    }

    [Fact]
    public void NavyVisibility_ScalesTheHitProfile()
    {
        var plain = Ship("capital_ship", ModifierSet.Empty);
        var hidden = Ship("capital_ship", new ModifierSet([new("navy_visibility", -0.2)]));

        Assert.Equal(HitChanceRules.ShipHitProfile(plain, defines) * 0.8, HitChanceRules.ShipHitProfile(hidden, defines), 9);
    }

    [Fact]
    public void TorpedoCooldownFactor_ScalesOnlyTheTorpedoSlot()
    {
        var ship = Ship("screen_ship", new ModifierSet([new("naval_torpedo_cooldown_factor", -0.25)]));

        Assert.Equal(3, WeaponCadenceRules.CooldownHours(WeaponSlot.Torpedo, defines, ship), 9);
        Assert.Equal(1, WeaponCadenceRules.CooldownHours(WeaponSlot.Light, defines, ship), 9);
        Assert.Equal(4, WeaponCadenceRules.CooldownHours(WeaponSlot.Torpedo, defines), 9);
    }

    [Fact]
    public void CasualtyOnHit_ScalesTheManpowerLoss()
    {
        var plain = Ship("capital_ship", ModifierSet.Empty);
        var drilled = Ship("capital_ship", new ModifierSet([new("navy_casualty_on_hit", -0.5)]));
        var immune = Ship("capital_ship", new ModifierSet([new("navy_casualty_on_hit", -1)]));

        var plainLoss = ExperienceRules.ApplyDamageLosses(plain, 100, defines);
        var drilledLoss = ExperienceRules.ApplyDamageLosses(drilled, 100, defines);

        Assert.Equal(Math.Round(plainLoss / 2.0, MidpointRounding.AwayFromZero), drilledLoss);
        Assert.Equal(0, ExperienceRules.ApplyDamageLosses(immune, 100, defines));
    }

    private static ModifierSet Modifiers()
    {
        return new ModifierSet([
            new("navy_capital_ship_attack_factor", 0.1), new("navy_capital_ship_defence_factor", 0.2),
            new("navy_screen_attack_factor", 0.3), new("navy_screen_defence_factor", 0.4),
            new("navy_submarine_attack_factor", 0.5), new("navy_submarine_defence_factor", 0.6)
        ]);
    }

    private static Ship Ship(string unitType, ModifierSet country)
    {
        var role = unitType switch
        {
            "capital_ship" => ShipRole.Capital,
            "submarine" => ShipRole.Submarine,
            "carrier" => ShipRole.Carrier,
            _ => ShipRole.Screen
        };
        var hull = new Hull("hull", "archetype", unitType, 1000, TestEntityFactory.CapitalStats() with { AntiAir = 10 });
        // Experience level 1, the base level: no experience bonus or malus.
        var experience = 0.2 * GameDefines.Vanilla().NNavy.NAVY_MAX_XP;
        var ship = new Ship("ship", new ShipDesign("design", hull, new ShipType(unitType, role, [unitType], 0, ["archetype"], [], 40), []), experience);
        ship.CountryModifiers = country;
        return ship;
    }
}
