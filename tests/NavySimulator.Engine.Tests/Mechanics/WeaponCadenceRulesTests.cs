using NavySimulator.Engine.Battles;
using NavySimulator.Engine.Battles.Mechanics;
using NavySimulator.Engine.Defines;
using NavySimulator.Engine.Tests.TestSupport;

namespace NavySimulator.Engine.Tests.Mechanics;

public class WeaponCadenceRulesTests
{
    private readonly GameDefines defines = GameDefines.Vanilla();

    [Fact]
    public void ActivationThreshold_WithCarriersPresent_UsesTheDefines()
    {
        var screen = TestEntityFactory.Screen("dd");
        var capital = TestEntityFactory.Capital("bb");
        var carrier = TestEntityFactory.Carrier("cv");
        var submarine = TestEntityFactory.Submarine("ss");
        submarine.RevealTimer = 5;
        Ship[] all = [screen, capital, carrier, submarine];

        Assert.Equal(0, WeaponCadenceRules.ActivationThreshold(carrier, all, defines));
        Assert.Equal(6, WeaponCadenceRules.ActivationThreshold(capital, all, defines));
        Assert.Equal(6, WeaponCadenceRules.ActivationThreshold(submarine, all, defines));
        Assert.Equal(8, WeaponCadenceRules.ActivationThreshold(screen, all, defines));
    }

    [Fact]
    public void ActivationThreshold_ShiftsDownWithoutCarriersOrCapitals()
    {
        var screen = TestEntityFactory.Screen("dd");
        var capital = TestEntityFactory.Capital("bb");

        // No carriers: capitals 0, screens ALL - (CAPITAL - CARRIER) = 2. No carriers or capitals: everything 0.
        Assert.Equal(0, WeaponCadenceRules.ActivationThreshold(capital, [screen, capital], defines));
        Assert.Equal(2, WeaponCadenceRules.ActivationThreshold(screen, [screen, capital], defines));
        Assert.Equal(0, WeaponCadenceRules.ActivationThreshold(screen, [screen], defines));
    }

    [Fact]
    public void ActivationThreshold_IgnoresHiddenSubmarines()
    {
        var screen = TestEntityFactory.Screen("dd");

        Assert.Equal(0, WeaponCadenceRules.ActivationThreshold(screen, [screen, TestEntityFactory.Submarine("hidden")], defines));
    }

    [Fact]
    public void IsActivated_IsStrictlyAboveTheThreshold()
    {
        var screen = TestEntityFactory.Screen("dd");
        Ship[] all = [screen, TestEntityFactory.Capital("bb")];

        Assert.False(WeaponCadenceRules.IsActivated(screen, 2, all, defines));
        Assert.True(WeaponCadenceRules.IsActivated(screen, 3, all, defines));
    }

    [Fact]
    public void Charge_GunsFireEveryHourAndTorpedoesEveryFourth()
    {
        var ship = TestEntityFactory.Screen("dd", torpedoAttack: 5);

        var lightHours = Enumerable.Range(0, 4).Where(_ => FiresThisHour(ship, WeaponSlot.Light)).Count();
        var torpedoHours = Enumerable.Range(1, 12).Where(_ => FiresThisHour(ship, WeaponSlot.Torpedo)).ToList();

        Assert.Equal(4, lightHours);
        Assert.Equal([4, 8, 12], torpedoHours);
    }

    [Fact]
    public void Charge_FractionalCooldownsCarryOverAndShortOnesFireSeveralTimes()
    {
        var modded = GameDefines.Vanilla();
        modded.NNavy.BASE_GUN_COOLDOWNS = [2.5, 4, 0.5];
        var ship = TestEntityFactory.Capital("bb");

        var heavyShots = Enumerable.Range(0, 10).Sum(_ => Shots(ship, WeaponSlot.Heavy, modded));
        var lightShotsFirstHour = Shots(ship, WeaponSlot.Light, modded);

        Assert.Equal(4, heavyShots);
        Assert.Equal(2, lightShotsFirstHour);
    }

    private bool FiresThisHour(Ship ship, WeaponSlot slot)
    {
        return Shots(ship, slot, defines) > 0;
    }

    private static int Shots(Ship ship, WeaponSlot slot, GameDefines defines)
    {
        if (!WeaponCadenceRules.Charge(ship, slot, defines))
        {
            return 0;
        }

        var shots = 0;

        while (WeaponCadenceRules.TrySpendShot(ship, slot, defines))
        {
            shots++;
        }

        return shots;
    }
}
