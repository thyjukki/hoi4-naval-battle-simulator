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
        var stats = TestEntityFactory.ScreenStats(lightAttack: 500) with { LightHitChanceFactor = 20 };
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
        var sunk = new HashSet<string>();

        foreach (var shot in result.Shots)
        {
            Assert.DoesNotContain(shot.TargetId, sunk);

            if (shot.KillingBlow)
            {
                sunk.Add(shot.TargetId);
            }
        }

        Assert.NotEmpty(sunk);
    }

    [Fact]
    public void Simulate_AnnihilationEndsBattle_EvenWhenContinueAfterRetreatIsOn()
    {
        var rules = new BattleRules { ContinueAfterRetreat = true, MaxHours = 500 };
        var scenario = TestEntityFactory.Scenario(Screens("a", 10, 50), Screens("d", 1, 1), rules);

        var result = new BattleEngine().Simulate(scenario, 1);

        Assert.Equal(BattleOutcome.AttackerVictory, result.Outcome);
        Assert.True(result.HoursFought < 500);
    }

    [Fact]
    public void Simulate_ScreensOnlyBattleStartsShootingInTheSecondHour()
    {
        // No carriers or capitals: every line activates at tick 1 and fires from the next tick (battle hour 1).
        var scenario = TestEntityFactory.Scenario(Screens("a", 2), Screens("d", 2), new BattleRules { MaxHours = 20 });

        var result = new BattleEngine().Simulate(scenario, 3);

        Assert.Equal(1, result.Shots.Min(shot => shot.Hour));
    }

    [Fact]
    public void Simulate_ScreensWaitTwoHoursLongerThanCapitalsWithoutCarriers()
    {
        var scenario = TestEntityFactory.Scenario(
            [TestEntityFactory.Capital("a_bb"), TestEntityFactory.Screen("a_dd")],
            [TestEntityFactory.Capital("d_bb"), TestEntityFactory.Screen("d_dd")],
            new BattleRules { MaxHours = 20 });

        var result = new BattleEngine().Simulate(scenario, 3);

        Assert.Equal(1, result.Shots.Where(shot => shot.ShooterId.EndsWith("bb")).Min(shot => shot.Hour));
        Assert.Equal(3, result.Shots.Where(shot => shot.ShooterId.EndsWith("dd")).Min(shot => shot.Hour));
    }

    [Fact]
    public void Simulate_AttackerFiresFirst_SoTheDefenderSunkInTheSameHourNeverShoots()
    {
        // Every roll is 0: every shot hits, and one hit sinks either ship.
        var scenario = TestEntityFactory.Scenario(Sharpshooters("a", 1), Sharpshooters("d", 1), new BattleRules { MaxHours = 20 });

        var result = new BattleEngine().Simulate(scenario, new ScriptedRandomSource(0.0));

        Assert.Equal(BattleOutcome.AttackerVictory, result.Outcome);
        Assert.All(result.Shots, shot => Assert.Equal("attacker", shot.ShooterSide));
    }

    [Fact]
    public void Simulate_MirrorMatchFavoursTheAttacker()
    {
        var engine = new BattleEngine();
        var attackerWins = 0;
        const int runs = 200;

        for (var seed = 0; seed < runs; seed++)
        {
            var scenario = TestEntityFactory.Scenario(Sharpshooters("a", 3), Sharpshooters("d", 3), new BattleRules { MaxHours = 300, RetreatEnabled = false });

            if (engine.Simulate(scenario, seed).Outcome == BattleOutcome.AttackerVictory)
            {
                attackerWins++;
            }
        }

        Assert.InRange(attackerWins / (double)runs, 0.65, 1.0);
    }

    [Fact]
    public void Simulate_DamagedShipsRetreatAndKeepFiring()
    {
        var engine = new BattleEngine();
        var scenario = TestEntityFactory.Scenario(Screens("a", 6, 3), Screens("d", 6, 3), new BattleRules { MaxHours = 400 });

        var result = engine.Simulate(scenario, 11);
        var retreatLines = result.HourlyLog.Where(line => line.Contains("is retreating")).ToList();

        Assert.NotEmpty(retreatLines);
        var retreatingShip = retreatLines[0].Split(' ')[3];
        var retreatHour = int.Parse(retreatLines[0].Split(' ')[1].TrimEnd(':'));
        Assert.Contains(result.Shots, shot => shot.ShooterId == retreatingShip && shot.Hour > retreatHour);
    }

    [Fact]
    public void Simulate_LowEngagementSubmarinesOnlyAttackTheCarrierLineWhileItHasShips()
    {
        var stats = TestEntityFactory.SubmarineStats() with { LightAttack = 3 };
        var submarine = TestEntityFactory.CreateShip("ss", ShipRole.Submarine, stats);
        var screen = TestEntityFactory.CreateShip("dd", ShipRole.Screen, TestEntityFactory.ScreenStats(lightAttack: 0) with { SubDetection = 0 });
        var convoy = TestEntityFactory.CreateShip("co", ShipRole.Convoy, TestEntityFactory.ScreenStats(lightAttack: 0) with { Hp = 5000 });
        var scenario = TestEntityFactory.Scenario([submarine], [screen, convoy], new BattleRules { MaxHours = 60 },
            attackerAggressiveness: Aggressiveness.EngageAtLowRisk);

        var result = new BattleEngine().Simulate(scenario, 5);
        var submarineShots = result.Shots.Where(shot => shot.ShooterId == "ss").ToList();
        var convoyEscapedHour = result.Ships.Single(ship => ship.ShipId == "co").RetreatedHour;

        // The convoy is fully screened, so the single torpedo roll never reaches it and the sub may not shoot the
        // screen either: it holds fire until the convoy has escaped and the minimum line falls back to the screens.
        Assert.NotNull(convoyEscapedHour);
        Assert.NotEmpty(submarineShots);
        Assert.All(submarineShots, shot => Assert.True(shot.Hour >= convoyEscapedHour, $"shot at hour {shot.Hour} before the convoy escaped"));
    }

    [Fact]
    public void Simulate_ShipsThatCannotSeeTheSubmarinesDisengage()
    {
        // No sub detection on the surface side: the submarine is never revealed, so the surface ship retreats.
        var blind = TestEntityFactory.CreateShip("dd", ShipRole.Screen, TestEntityFactory.ScreenStats() with { SubDetection = 0, DepthChargeAttack = 10 });
        var scenario = TestEntityFactory.Scenario([blind], [TestEntityFactory.Submarine("ss")], new BattleRules { MaxHours = 200 });

        var result = new BattleEngine().Simulate(scenario, 2);

        Assert.DoesNotContain(result.Shots, shot => shot.ShooterId == "dd");
        Assert.Contains(result.HourlyLog, line => line.Contains("disengages"));
    }

    private static PlaneEquipment NavalBomber() =>
        new("naval_bomber", new PlaneStats(AirDefense: 10, AirAttack: 5, NavalAttack: 20, Speed: 400, Agility: 40, NavalTargeting: 10, Reliability: 0.8),
            ["naval_bomber"], ["naval_bomber", "port_strike"], carrierCapable: true);

    private static BattleScenario AirScenario(BattleParticipant attacker, IEnumerable<Ship> defenderShips, BattleRules rules)
    {
        return new BattleScenario("air", "ocean", "clear", 1, null, attacker, TestEntityFactory.Participant("defender_fleet", defenderShips),
            new Dictionary<string, PlaneEquipment> { ["naval_bomber"] = NavalBomber() }, Defines.GameDefines.Vanilla(), rules);
    }

    [Fact]
    public void Simulate_CarrierPlanesStrikeOnlyAtTheRegionsAirHours()
    {
        var carrier = TestEntityFactory.CreateShip("cv", ShipRole.Carrier, TestEntityFactory.CapitalStats() with { HeavyAttack = 0, LightAttack = 0, CarrierSize = 4 });
        var wings = new Dictionary<string, List<CarrierAirwingAssignment>>
        {
            [carrier.ID] = [new CarrierAirwingAssignment("naval_bomber", 4, 10)]
        };
        var attacker = new BattleParticipant(new Fleet("attacker_fleet", [carrier], wings), null, [], []);
        var rules = new BattleRules { MaxHours = 48, StartHourOfDay = 12, AirRegionPhase = 4, NightEnabled = false };

        var result = new BattleEngine().Simulate(AirScenario(attacker, [TestEntityFactory.Capital("bb")], rules), 9);

        Assert.NotEmpty(result.AirStrikes);
        Assert.All(result.AirStrikes, strike => Assert.Equal(4, (12 + strike.Hour) % 8));
        Assert.Contains(result.AirStrikes, strike => strike.HpDamage > 0);
    }

    [Fact]
    public void Simulate_LandBasedPlanesDoDamageWithoutCarriers()
    {
        var attacker = new BattleParticipant(new Fleet("attacker_fleet", [TestEntityFactory.Screen("dd", lightAttack: 0)]), null,
            [], [], landBasedAirwings: [new LandBasedAirwing("naval_bomber", 100)]);
        var rules = new BattleRules { MaxHours = 48, NightEnabled = false };

        var result = new BattleEngine().Simulate(AirScenario(attacker, [TestEntityFactory.Capital("bb")], rules), 4);

        Assert.Contains(result.AirStrikes, strike => strike.Source == "land-based naval_bomber" && strike.HpDamage > 0);
    }

    [Fact]
    public void Simulate_PlanesOfASunkCarrierStopStriking()
    {
        var carrier = TestEntityFactory.CreateShip("cv", ShipRole.Carrier, TestEntityFactory.CapitalStats() with { HeavyAttack = 0, LightAttack = 0, CarrierSize = 4 });
        var wings = new Dictionary<string, List<CarrierAirwingAssignment>>
        {
            [carrier.ID] = [new CarrierAirwingAssignment("naval_bomber", 4, 10)]
        };
        var attacker = new BattleParticipant(new Fleet("attacker_fleet", [carrier], wings), null, [], []);
        var rules = new BattleRules { MaxHours = 200, NightEnabled = false, RetreatEnabled = false };

        var result = new BattleEngine().Simulate(AirScenario(attacker, Sharpshooters("d", 2), rules), 1);
        var sunkHour = result.Ships.Single(ship => ship.ShipId == "cv").SunkHour;

        Assert.NotNull(sunkHour);
        Assert.DoesNotContain(result.AirStrikes, strike => strike.Hour > sunkHour);
    }

    [Fact]
    public void Simulate_RejectsSharedFleetInstance()
    {
        var fleet = new Fleet("shared", Screens("s", 2));
        var participant = new BattleParticipant(fleet, null, [], []);
        var scenario = new BattleScenario("t", "ocean", "clear", 1, null, participant, participant,
            new Dictionary<string, PlaneEquipment>(), Defines.GameDefines.Vanilla(), new BattleRules());

        Assert.Throws<ArgumentException>(() => new BattleEngine().Simulate(scenario, 1));
    }

    [Theory]
    [InlineData(true, "Weather 'rain_heavy' has no effect yet.")]
    [InlineData(false, "Weather 'rain_heavy' is ignored: weather is off.")]
    public void Simulate_NotesTheWeatherBySwitch(bool weatherEnabled, string expectedNote)
    {
        var scenario = new BattleScenario("t", "ocean", "rain_heavy", 1, null,
            TestEntityFactory.Participant("attacker_fleet", Screens("a", 2)), TestEntityFactory.Participant("defender_fleet", Screens("d", 2)),
            new Dictionary<string, PlaneEquipment>(), Defines.GameDefines.Vanilla(), new BattleRules { MaxHours = 5, WeatherEnabled = weatherEnabled });

        var result = new BattleEngine().Simulate(scenario, 1);

        Assert.Contains(expectedNote, result.Notes);
    }
}
