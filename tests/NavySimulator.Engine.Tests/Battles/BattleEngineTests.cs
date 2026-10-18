using NavySimulator.Engine.Battles;
using NavySimulator.Engine.Results;
using NavySimulator.Engine.Tests.TestSupport;

namespace NavySimulator.Engine.Tests.Battles;

public class BattleEngineTests
{
    private static List<Ship> Screens(string prefix, int count, double lightAttack = 5)
    {
        return Enumerable.Range(1, count).Select(i => TestEntityFactory.Screen($"{prefix}{i:D2}", lightAttack)).ToList();
    }

    private static List<Ship> Sharpshooters(string prefix, int count)
    {
        var stats = TestEntityFactory.ScreenStats(lightAttack: 500) with { LightHitChanceFactor = 9 };
        return Enumerable.Range(1, count).Select(i => TestEntityFactory.CreateShip($"{prefix}{i:D2}", ShipRole.Screen, stats)).ToList();
    }

    [Fact]
    public void Simulate_SameSeedGivesIdenticalBattle()
    {
        var rules = new BattleRules { MaxHours = 200 };
        var scenario = TestEntityFactory.Scenario(Screens("a", 6), [.. Screens("d", 4), TestEntityFactory.Capital("bb")], rules);
        var engine = new BattleEngine();

        var first = engine.Simulate(scenario, 42);
        var second = engine.Simulate(scenario, 42);

        Assert.Equal(first.Outcome, second.Outcome);
        Assert.Equal(first.HoursFought, second.HoursFought);
        Assert.Equal(first.Shots, second.Shots);
        Assert.Equal(first.Ships, second.Ships);
    }

    [Fact]
    public void Simulate_NoShipIsTargetedAfterItSank()
    {
        var scenario = TestEntityFactory.Scenario(Screens("a", 8, 30), Screens("d", 8, 30), new BattleRules { MaxHours = 200 });

        var result = new BattleEngine().Simulate(scenario, 7);
        var sunkAtShot = new Dictionary<string, int>();

        for (var i = 0; i < result.Shots.Count; i++)
        {
            var shot = result.Shots[i];
            Assert.False(sunkAtShot.ContainsKey(shot.TargetId), $"shot {i} targets {shot.TargetId}, which already sank");

            if (shot.KillingBlow)
            {
                sunkAtShot[shot.TargetId] = i;
            }
        }

        Assert.NotEmpty(sunkAtShot);
    }

    [Fact]
    public void Simulate_AnnihilationEndsBattle_EvenWhenContinueAfterRetreatIsOn()
    {
        var rules = new BattleRules { ContinueAfterRetreat = true, MaxHours = 500 };
        var scenario = TestEntityFactory.Scenario(Screens("a", 10, 50), Screens("d", 1, 1), rules);

        var result = new BattleEngine().Simulate(scenario, 1);

        Assert.Equal(BattleOutcome.AttackerVictory, result.Outcome);
        Assert.True(result.HoursFought < 500);
        Assert.Equal(0, result.Reengagements);
    }

    [Fact]
    public void Simulate_FirstShotsHappenWhenScreensActivate()
    {
        var scenario = TestEntityFactory.Scenario(Screens("a", 2), Screens("d", 2), new BattleRules { MaxHours = 20 });

        var result = new BattleEngine().Simulate(scenario, 3);

        Assert.Equal(8, result.Shots.Min(shot => shot.Hour));
    }

    [Fact]
    public void Simulate_SimultaneousFiringLetsBothShipsSinkEachOther()
    {
        // Every roll is 0: all shots hit and pick the first candidate; one hit is enough to sink either ship.
        var rules = new BattleRules { FiringOrder = FiringOrder.Simultaneous, MaxHours = 20 };
        var scenario = TestEntityFactory.Scenario(Screens("a", 1, 500), Screens("d", 1, 500), rules);

        var result = new BattleEngine().Simulate(scenario, new ScriptedRandomSource(0.0));

        Assert.Equal(BattleOutcome.BothSidesGone, result.Outcome);
    }

    [Fact]
    public void Simulate_InterleavedFiringLetsOnlyTheFirstShooterSurvive()
    {
        var rules = new BattleRules { FiringOrder = FiringOrder.Interleaved, MaxHours = 20 };
        var scenario = TestEntityFactory.Scenario(Screens("a", 1, 500), Screens("d", 1, 500), rules);

        var result = new BattleEngine().Simulate(scenario, new ScriptedRandomSource(0.0));

        Assert.NotEqual(BattleOutcome.BothSidesGone, result.Outcome);
        Assert.Single(result.Shots);
    }

    [Theory]
    [InlineData(FiringOrder.Interleaved, 0.35, 0.65)]
    [InlineData(FiringOrder.Simultaneous, 0.35, 0.65)]
    [InlineData(FiringOrder.AttackerFirst, 0.65, 1.0)]
    public void Simulate_MirrorMatchWinRateDependsOnFiringOrder(FiringOrder firingOrder, double minAttackerWinShare, double maxAttackerWinShare)
    {
        var engine = new BattleEngine();
        var attackerWins = 0;
        const int runs = 200;

        for (var seed = 0; seed < runs; seed++)
        {
            var rules = new BattleRules { FiringOrder = firingOrder, MaxHours = 300, RetreatEnabled = false };
            // Nearly every shot hits and one hit sinks a ship, so whoever shoots first in an hour wins unless the order is fair.
            var scenario = TestEntityFactory.Scenario(Sharpshooters("a", 3), Sharpshooters("d", 3), rules);
            var result = engine.Simulate(scenario, seed);

            if (result.Outcome == BattleOutcome.AttackerVictory)
            {
                attackerWins++;
            }
        }

        Assert.InRange(attackerWins / (double)runs, minAttackerWinShare, maxAttackerWinShare);
    }

    [Fact]
    public void Simulate_DamagedShipsRetreat_AndRetreatIsSeeded()
    {
        var rules = new BattleRules { MaxHours = 400 };
        var engine = new BattleEngine();

        BattleResult Run()
        {
            var scenario = TestEntityFactory.Scenario(Screens("a", 6, 3), Screens("d", 6, 3), rules);
            return engine.Simulate(scenario, 11);
        }

        var first = Run();
        var second = Run();

        Assert.Contains(first.HourlyLog, line => line.Contains("is retreating"));
        Assert.Equal(first.Ships, second.Ships);
    }

    [Fact]
    public void Simulate_RejectsSharedFleetInstance()
    {
        var fleet = new Fleet("shared", Screens("s", 2));
        var participant = new BattleParticipant(fleet, string.Empty, string.Empty, 0, [], []);
        var scenario = new BattleScenario("t", "ocean", "clear", 1, null, participant, participant,
            new Dictionary<string, PlaneEquipment>(), Defines.GameDefines.Vanilla(), new BattleRules());

        Assert.Throws<ArgumentException>(() => new BattleEngine().Simulate(scenario, 1));
    }
}
