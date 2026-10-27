using NavySimulator.Engine.Battles;
using NavySimulator.Engine.Battles.Mechanics;
using NavySimulator.Engine.Defines;
using NavySimulator.Engine.Random;
using NavySimulator.Engine.Results;
using NavySimulator.Engine.Stats;
using NavySimulator.Engine.Tests.TestSupport;

namespace NavySimulator.Engine.Tests.Mechanics;

/// <summary>S-RECOVERY: the ship recovery chance at a sinking (0xd77ff0).</summary>
public class RecoveryRulesTests
{
    private readonly GameDefines defines = GameDefines.Vanilla();

    [Fact]
    public void WithoutAFactor_TheGameRollsButNeverRecovers()
    {
        // [RE + LOG] chance × naval_ship_recovery_chance_factor itself: 0 when nothing sets the factor (every country in
        // 1.19.3), so the roll is made and fails even at 0.
        var (side, ships) = Side(chance: 0.5, factor: null);
        var random = new RecordingRandomSource(1);
        ships[0].CurrentHP = 0;

        Assert.False(RecoveryRules.TryRecover(ships[0], side, defines, random));
        Assert.Equal([RollKind.ShipRecovery], random.Rolls.Select(roll => roll.Kind));
        Assert.True(ships[0].IsSunk);
        Assert.False(ships[0].IsRecovered);

        // Without any chance there is no roll.
        var (plain, others) = Side(chance: null, factor: null);
        var none = new RecordingRandomSource(1);
        others[0].CurrentHP = 0;
        Assert.False(RecoveryRules.TryRecover(others[0], plain, defines, none));
        Assert.Empty(none.Rolls);
    }

    [Fact]
    public void ARecoveredShip_GetsAShareOfItsTaskForcesMaximumHp_AndStaysOutOfTheBattle()
    {
        // [RE + LOG] 0xd781e4: HP = 0.01 × the summed maximum HP of the ships still in the task force (the sunk one
        // included, an earlier loss not), at most the ship's own maximum.
        var (side, ships) = Side(chance: 0.5, factor: 1);
        ships[1].CurrentHP = 0;
        ships[0].CurrentHP = 0;

        Assert.True(RecoveryRules.TryRecover(ships[0], side, defines, new ScriptedRandomSource(0.49999)));
        Assert.Equal(0.01 * (100 + 5000), ships[0].CurrentHP, 9);
        Assert.True(ships[0].IsRecovered);
        Assert.True(ships[0].IsSunk);
        Assert.False(ships[0].IsInBattle);

        // The roll must be below the final chance; and the HP is capped at the ship's maximum.
        var (big, fleet) = Side(chance: 0.5, factor: 1, flagshipHp: 50000);
        fleet[0].CurrentHP = 0;
        Assert.False(RecoveryRules.TryRecover(fleet[0], big, defines, new ScriptedRandomSource(0.5)));
        Assert.True(RecoveryRules.TryRecover(fleet[1], big, defines, new ScriptedRandomSource(0.0)));
        Assert.Equal(100, fleet[1].CurrentHP, 9);
    }

    [Fact]
    public void Simulate_ReportsRecoveredShipsApart_FromTheSunk()
    {
        var attacker = TestEntityFactory.Participant("a", [TestEntityFactory.Capital("bb1"), TestEntityFactory.Capital("bb2")]);
        var (defender, _) = Participant(chance: 1, factor: 1, flagshipHp: 100);
        var scenario = new BattleScenario("recovery", "ocean", "clear", 1, null, attacker, defender, new Dictionary<string, PlaneEquipment>(),
            defines, new BattleRules { MaxHours = 200, NightEnabled = false });

        var result = new BattleEngine().Simulate(scenario, 1);

        var recovered = result.Ships.Where(ship => ship.Recovered).ToList();
        Assert.NotEmpty(recovered);
        Assert.All(recovered, ship => Assert.False(ship.Sunk));
        Assert.All(recovered, ship => Assert.True(ship.FinalHp > 0));
        Assert.Equal(recovered.Count, result.Defender.ShipsRecovered);
        Assert.Contains(result.Events, battleEvent => battleEvent.Kind == BattleEventKind.Recovered);
    }

    private (BattleSide Side, List<Ship> Ships) Side(double? chance, double? factor, double flagshipHp = 5000)
    {
        var (participant, ships) = Participant(chance, factor, flagshipHp);
        return (new BattleSide("defender", participant), ships);
    }

    private static (BattleParticipant Participant, List<Ship> Ships) Participant(double? chance, double? factor, double flagshipHp)
    {
        var ships = new List<Ship>
        {
            TestEntityFactory.Screen("cl1"),
            TestEntityFactory.Screen("cl2"),
            TestEntityFactory.CreateShip("bb", ShipRole.Capital, TestEntityFactory.CapitalStats() with { Hp = flagshipHp })
        };
        var modifiers = new List<KeyValuePair<string, double>>();

        if (chance is { } value)
        {
            modifiers.Add(new(StaticModifiers.NavalShipRecoveryChance, value));
        }

        if (factor is { } scale)
        {
            modifiers.Add(new(StaticModifiers.NavalShipRecoveryChanceFactor, scale));
        }

        var research = new Research("recovery", new ShipStats(), new ShipStats(), new ShipStats(), [], [], false, new ModifierSet(modifiers));
        return (new BattleParticipant(new Fleet("d", ships), null, [research], []), ships);
    }
}
