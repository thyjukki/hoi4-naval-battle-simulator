using NavySimulator.Engine.Battles;
using NavySimulator.Engine.Battles.Mechanics;
using NavySimulator.Engine.Defines;
using NavySimulator.Engine.Tests.TestSupport;

namespace NavySimulator.Engine.Tests.Mechanics;

public class WeaponCadenceRulesTests
{
    private readonly GameDefines defines = GameDefines.Vanilla();

    [Theory]
    [InlineData(ShipRole.Carrier, 0)]
    [InlineData(ShipRole.Capital, 6)]
    [InlineData(ShipRole.Screen, 8)]
    [InlineData(ShipRole.Submarine, 8)]
    public void ActivationHour_UsesWikiDelaysRegardlessOfComposition(ShipRole role, int expected)
    {
        Assert.Equal(expected, WeaponCadenceRules.ActivationHour(role, false, false, defines, new BattleRules()));
    }

    [Fact]
    public void ActivationHour_CanDependOnComposition()
    {
        var rules = new BattleRules { ActivationDependsOnComposition = true };

        Assert.Equal(0, WeaponCadenceRules.ActivationHour(ShipRole.Capital, false, true, defines, rules));
        Assert.Equal(6, WeaponCadenceRules.ActivationHour(ShipRole.Screen, false, true, defines, rules));
        Assert.Equal(8, WeaponCadenceRules.ActivationHour(ShipRole.Screen, true, true, defines, rules));
    }

    [Fact]
    public void TryFire_LightGunsFireEveryHourAndTorpedoesEveryFourHours()
    {
        var ship = TestEntityFactory.Screen("dd", torpedoAttack: 5);
        var rules = new BattleRules();

        var lightHours = Enumerable.Range(0, 14).Where(hour => WeaponCadenceRules.TryFire(ship, WeaponType.Light, hour, 8, defines, rules) is null);
        var torpedoHours = Enumerable.Range(0, 17).Where(hour => WeaponCadenceRules.TryFire(ship, WeaponType.Torpedo, hour, 8, defines, rules) is null);

        Assert.Equal([8, 9, 10, 11, 12, 13], lightHours);
        Assert.Equal([8, 12, 16], torpedoHours);
    }

    [Fact]
    public void TryFire_CanWaitOneCooldownBeforeFirstShot()
    {
        var ship = TestEntityFactory.Screen("dd", torpedoAttack: 5);
        var rules = new BattleRules { FirstShotWaitsForCooldown = true };

        var torpedoHours = Enumerable.Range(0, 17).Where(hour => WeaponCadenceRules.TryFire(ship, WeaponType.Torpedo, hour, 8, defines, rules) is null);

        Assert.Equal([12, 16], torpedoHours);
    }

    [Fact]
    public void TryFire_ReportsSkipReasons()
    {
        var ship = TestEntityFactory.Screen("dd", torpedoAttack: 5);

        Assert.Equal(SkipReasons.LineNotActive, WeaponCadenceRules.TryFire(ship, WeaponType.Torpedo, 7, 8, defines, new BattleRules()));
        Assert.Null(WeaponCadenceRules.TryFire(ship, WeaponType.Torpedo, 8, 8, defines, new BattleRules()));
        Assert.Equal(SkipReasons.Cooldown, WeaponCadenceRules.TryFire(ship, WeaponType.Torpedo, 9, 8, defines, new BattleRules()));
    }
}
