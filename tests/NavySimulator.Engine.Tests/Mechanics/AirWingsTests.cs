using NavySimulator.Engine.Battles;
using NavySimulator.Engine.Battles.Air;
using NavySimulator.Engine.Defines;
using NavySimulator.Engine.Environment;
using NavySimulator.Engine.Random;
using NavySimulator.Engine.Stats;
using NavySimulator.Engine.Tests.TestSupport;

namespace NavySimulator.Engine.Tests.Mechanics;

/// <summary>A-WINGS: wing roles, air objects per wing pool, land-based joins and daily reinforcement.</summary>
public class AirWingsTests
{
    private readonly GameDefines defines = GameDefines.Vanilla();

    private static PlaneEquipment CarrierBomber(string id = "cv_nb") => new(id, new PlaneStats(AirDefense: 13, NavalAttack: 10, Agility: 30, NavalTargeting: 10),
        ["naval_bomber"], ["naval_bomber", "port_strike", "naval_patrol"], true, "cv_nav_bomber", ["naval_bomber"]);

    private static PlaneEquipment CarrierFighter() => new("cv_f", new PlaneStats(AirAttack: 24, AirDefense: 11, Agility: 50, Speed: 390),
        ["fighter", "interceptor"], ["air_superiority", "interception", "naval_patrol"], true, "cv_fighter", ["fighter"]);

    // TFB: the equipment is { cas fighter }, the sub-unit { cas strategic_bomber }.
    private static PlaneEquipment CarrierCas() => new("cv_cas", new PlaneStats(NavalAttack: 7.5, NavalTargeting: 7.5),
        ["cas", "fighter"], ["cas", "naval_bomber", "port_strike"], true, "cv_cas", ["cas", "strategic_bomber"]);

    private static PlaneEquipment LandBomber(string id = "nb") => new(id, new PlaneStats(AirDefense: 12, NavalAttack: 15, Agility: 30, NavalTargeting: 10),
        ["naval_bomber"], ["naval_bomber", "port_strike"], false, "nav_bomber", ["naval_bomber"]);

    [Fact]
    public void Roles_FighterTestsReadTheSubUnit_AndTheSortieTestLeavesOutInterceptors()
    {
        var values = defines.Hardcoded;
        var interceptor = new PlaneEquipment("ri", new PlaneStats(), ["interceptor"], ["interception"], false, "rocket_interceptor", ["interceptor"]);

        Assert.False(CarrierCas().IsFighterType(values));
        Assert.True(CarrierCas().IsInStrikeList(values));
        Assert.True(CarrierFighter().IsFighterType(values));
        Assert.False(CarrierFighter().IsInStrikeList(values));
        Assert.True(interceptor.IsFighterType(values));
        Assert.False(interceptor.IsSortieFighterType(values));

        // A port-strike-only plane is in the strike list but cannot execute a naval strike.
        var wing = new AirWing(new PlaneEquipment("sb", new PlaneStats(), ["strategic_bomber"], ["strategic_bomber", "port_strike"]), 10, values);
        Assert.True(wing.IsStrikeWing);
        Assert.False(wing.ExecutesNavalStrike);
    }

    [Fact]
    public void CarrierGroups_AreOnePerWingPool_AndCrowdingCountsTheWholeCarrier()
    {
        var carrier = Carrier("cv", deckSize: 3);
        var phase = Phase([carrier], wings: [new("cv_f", 1, 10), new("cv_nb", 2, 10), new("cv_f", 1, 10)]);
        var groups = phase.GroupsOf(phase.Attacker);

        Assert.Equal(["cv_fighter", "cv_nav_bomber"], groups.Select(group => group.Pool));
        Assert.Equal([20, 20], groups.Select(group => group.Planes));
        Assert.Equal(40, phase.CarrierPlanes(carrier));

        // 40 planes on a 30-plane deck: crowd 1 - CAPACITY_PENALTY * (10 / 30) for every pool, the bomber pool's 20 included.
        var bombers = groups[1];
        var crowded = phase.SortieEfficiency(bombers, phase.Attacker, 30, BattleEnvironment.None);
        var alone = AirRules.SortieEfficiency(bombers, phase.Attacker.Screening, true, 2, 30, BattleEnvironment.None, defines);
        Assert.Equal(alone * (1 - defines.NAir.CAPACITY_PENALTY / 3), crowded, 9);
    }

    [Fact]
    public void LandBased_JoinUpToTheCapEachAirHour_InListedOrder()
    {
        // One small enemy screen: the cap is NAVAL_COMBAT_EXTERNAL_PLANES_MIN_CAP.
        var cap = (int)defines.NAir.NAVAL_COMBAT_EXTERNAL_PLANES_MIN_CAP;
        var phase = Phase([TestEntityFactory.Screen("dd", lightAttack: 0)],
            landBased: [new LandBasedAirwing("nb", cap + 30, 0.5), new LandBasedAirwing("nb2", 50)]);
        var groups = phase.GroupsOf(phase.Attacker);

        phase.JoinLandBased(0);

        // The first base offers round((cap + 30) * 0.5) planes; when that fills the cap the second does not join.
        Assert.True(groups[0].HasJoined);
        Assert.Equal(Math.Min(cap, (int)Math.Round((cap + 30) * 0.5, MidpointRounding.AwayFromZero)), groups[0].Offered);
        Assert.Equal((cap + 30) * 0.5 < cap, groups[1].HasJoined);
        Assert.Equal(cap + 30, groups[0].Planes);
    }

    [Fact]
    public void ReinforceDaily_RefillsLandWingsFromTheStockpile_ButNotCarrierWings()
    {
        var carrier = Carrier("cv");
        var phase = Phase([carrier], wings: [new("cv_nb", 1, 10)], landBased: [new LandBasedAirwing("nb", 100)],
            stockpile: new Dictionary<string, int> { ["nb"] = 25, ["nb2"] = 40, ["cv_nb"] = 50 });
        var groups = phase.GroupsOf(phase.Attacker);
        groups[0].Wings[0].Planes = 4;
        groups[1].Wings[0].Planes = 30;

        var lines = phase.ReinforceDaily(phase.Attacker);

        // 70 missing: the stock in the game's order (newest first; equal here, so as listed): 25 of its own plane, then 40 of
        // the other naval bomber of its sub-unit, kept as a second variant (A-MIXED).
        Assert.Equal(95, groups[1].Planes);
        Assert.Equal(4, groups[0].Planes);
        Assert.Equal(0, phase.StockpileOf(phase.Attacker)["nb"]);
        Assert.Equal(0, phase.StockpileOf(phase.Attacker)["nb2"]);
        Assert.Equal(50, phase.StockpileOf(phase.Attacker)["cv_nb"]);
        Assert.Equal(["attacker land-based nb refilled 65 planes from the stockpile"], lines);
    }

    [Fact]
    public void Accidents_HitOneWingPerBaseInAPassedHour_AndAccumulateTheScore()
    {
        // A-ACCIDENT [RE]: reliability 0 → trunc(0.1 × 2) × (1 + 0.5 experience malus) = 0.3; the gate passes (5,000 <
        // 10,000), the wing's draw 0 is below it, and the effect 0.99999 adds trunc(1000 × 0.99999 × 0.007) = 6.99993: 6 lost.
        var phase = Phase([TestEntityFactory.Capital("bb")], landBased: [new LandBasedAirwing("nb", 1000)]);
        var wing = phase.GroupsOf(phase.Attacker)[0].Wings[0];

        var lines = phase.Accidents(5, new ScriptedRandomSource(0.05, 0.0, 0.0, 0.99999, 0.5));

        Assert.Equal(994, wing.Planes);
        Assert.Equal(["Hour 5: attacker land-based nb lost 6 nb in an accident"], lines);

        // An hour whose gate draw is 10 % or more has none.
        Assert.Empty(phase.Accidents(6, new ScriptedRandomSource(0.2)));
        Assert.Equal(994, wing.Planes);

        // Switched off (a constant-roll replay, S-REPLAY-CONSTANT): no draw, no loss.
        phase.AccidentsEnabled = false;
        Assert.Empty(phase.Accidents(7, new ScriptedRandomSource()));
        Assert.Equal(994, wing.Planes);
    }

    [Fact]
    public void Simulate_WithoutCarrierNavalStrikeMissions_CarriersDoNoDamage()
    {
        var withMission = Simulate(new BattleRules { MaxHours = 48, NightEnabled = false });
        var without = Simulate(new BattleRules { MaxHours = 48, NightEnabled = false, CarrierWingsExecuteNavalStrike = false });

        Assert.Contains(withMission.AirStrikes, strike => strike.HpDamage > 0);
        Assert.NotEmpty(without.AirStrikes);
        Assert.All(without.AirStrikes, strike => Assert.Equal(0, strike.HpDamage));
    }

    [Fact]
    public void CarrierSortieExperience_LeavesTheStanceRatioAnHourBeforeTheStrikes()
    {
        // [LOG] The gain comes after the hour ends, with the game's counter two ahead: battle hour 21 still gains from the
        // stance ratio (1.0 at index 4), hour 22 from BASE_CARRIER_SORTIE_EFFICIENCY (0.5), half as much.
        var carrier = TestEntityFactory.CreateShip("cv", ShipRole.Carrier, TestEntityFactory.CapitalStats() with { HeavyAttack = 0, LightAttack = 0, CarrierSize = 4 });
        var wings = new Dictionary<string, List<CarrierAirwingAssignment>> { [carrier.ID] = [new CarrierAirwingAssignment("cv_nb", 4, 10)] };
        var attacker = new BattleParticipant(new Fleet("a", [carrier], wings), null, [], [], carrierStanceIndex: 4);
        var target = TestEntityFactory.CreateShip("target", ShipRole.Capital,
            TestEntityFactory.CapitalStats() with { HeavyAttack = 0, LightAttack = 0, TorpedoAttack = 0, Hp = 100000 });
        var scenario = new BattleScenario("air", "ocean", "clear", 1, null, attacker, TestEntityFactory.Participant("d", [target]),
            Planes(), defines, new BattleRules { MaxHours = 30, NightEnabled = false });
        var session = new BattleSession(scenario, new ConstantRandomSource(0.5));
        var experience = new List<double>();

        while (session.HoursFought < 25)
        {
            experience.Add(carrier.Experience);
            session.StepHour();
        }

        double Gain(int hour) => experience[hour + 1] - experience[hour];
        Assert.Equal(Gain(20), Gain(21), 9);
        Assert.Equal(Gain(21) / 2, Gain(22), 9);
        Assert.Equal(Gain(22), Gain(23), 9);
    }

    [Fact]
    public void RefilledPlanes_BringTheRegionChangePenalty_WhichDecaysEveryHour()
    {
        // A-COVERAGE-REFILL [RE + LOG]: the logged BUL tactical bombers, 76 planes refilled with 18 at the day change:
        // pen = trunc(18 × 0.9 / 94) = 0.17234; six hours at trunc(0.192 / 24) = 0.008 later 0.12434 (effectiveness 0.87566).
        var tfb = GameDefines.Vanilla();
        var bomber = new PlaneEquipment("tac", new PlaneStats(AirAttack: 1), ["tactical_bomber"], ["naval_bomber"], subUnitTypes: ["tactical_bomber"]);
        var wing = new AirWing(bomber, 76, tfb.Hardcoded);

        wing.Reinforce(bomber, 18, tfb);
        Assert.Equal(0.17234, wing.RegionChangePenalty, 9);

        for (var hour = 0; hour < 6; hour++)
        {
            wing.DecayRegionChangePenalty(tfb);
        }

        Assert.Equal(0.12434, wing.RegionChangePenalty, 9);

        // Setting the count (a replay's logged planes) brings none.
        wing.Planes += 10;
        Assert.Equal(0.12434, wing.RegionChangePenalty, 9);
    }

    [Fact]
    public void RegionEscorts_CountACarrierWingOnBothListsTwice_AndALandWingOnce()
    {
        // A-REGION [RE + LOG] 0xc4c570: the taken and escort sums run over the region list and the scrambled list, so the
        // share left is 1 − 2E / (2C + L) for carrier wings C and E on both and a land wing L on the region list only.
        var carrier = TestEntityFactory.CreateShip("cv", ShipRole.Carrier, TestEntityFactory.CapitalStats() with { HeavyAttack = 0, LightAttack = 0, CarrierSize = 40 });
        var wings = new Dictionary<string, List<CarrierAirwingAssignment>>
        {
            [carrier.ID] = [new CarrierAirwingAssignment("cv_nb", 2, 10), new CarrierAirwingAssignment("cv_f", 1, 10)]
        };
        var attacker = new BattleParticipant(new Fleet("a", [carrier], wings), null, [], [], carrierStanceIndex: 4,
            landBasedAirwings: [new LandBasedAirwing("nb", 50)], carrierWingsOnRegionMission: true);
        var defender = new BattleParticipant(new Fleet("d", [TestEntityFactory.Capital("bb")]), null, [], [],
            landBasedAirwings: [new LandBasedAirwing("cv_f", 100, Mission: LandBasedAirwing.AirSuperiority)]);
        var scenario = new BattleScenario("air", "ocean", "clear", 1, null, attacker, defender, Planes(), defines, new BattleRules());
        var phase = new AirPhase(scenario, new BattleSide("attacker", attacker), new BattleSide("defender", defender));

        phase.RegionAirWar(1, new ConstantRandomSource(0.5));

        var groups = phase.GroupsOf(phase.Attacker);
        var carrierWings = groups.Where(group => group.Carrier is not null).SelectMany(group => group.Wings).ToList();
        var land = groups.Single(group => group.LandBased is not null).Wings.Single();
        var bomber = carrierWings.First(wing => wing.ExecutesNavalStrike);
        var fighter = carrierWings.Single(wing => wing.IsFighter);
        var country = attacker.CountryModifiers;
        var escort = RegionAirWarRules.Escort(fighter.Planes, RegionAirWarRules.Stats(fighter, defines, country), defines, country,
            phase.RegionDetection[phase.Attacker]);
        var carrierTaken = carrierWings.Sum(wing => wing.DisruptionTaken);
        var left = 1 - Math.Min(1, 2 * escort / (2 * carrierTaken + land.DisruptionTaken));

        Assert.InRange(left, 0.01, 0.99);
        Assert.True(land.DisruptionTaken > 0);
        Assert.Equal(FixedPoint.Truncate(left * bomber.DisruptionTaken), bomber.Disruption, 9);
    }

    private Results.BattleResult Simulate(BattleRules rules)
    {
        var carrier = TestEntityFactory.CreateShip("cv", ShipRole.Carrier, TestEntityFactory.CapitalStats() with { HeavyAttack = 0, LightAttack = 0, CarrierSize = 4 });
        var wings = new Dictionary<string, List<CarrierAirwingAssignment>> { [carrier.ID] = [new CarrierAirwingAssignment("cv_nb", 4, 10)] };
        var attacker = new BattleParticipant(new Fleet("a", [carrier], wings), null, [], []);
        var scenario = new BattleScenario("air", "ocean", "clear", 1, null, attacker, TestEntityFactory.Participant("d", [TestEntityFactory.Capital("bb")]),
            Planes(), defines, rules);
        return new BattleEngine().Simulate(scenario, 3);
    }

    private static Ship Carrier(string id, double deckSize = 4)
    {
        return TestEntityFactory.CreateShip(id, ShipRole.Carrier, TestEntityFactory.CapitalStats() with { HeavyAttack = 0, CarrierSize = deckSize });
    }

    private static Dictionary<string, PlaneEquipment> Planes()
    {
        return new[] { CarrierBomber(), CarrierFighter(), CarrierCas(), LandBomber(), LandBomber("nb2") }.ToDictionary(plane => plane.ID);
    }

    private AirPhase Phase(
        List<Ship> ships,
        List<CarrierAirwingAssignment>? wings = null,
        List<LandBasedAirwing>? landBased = null,
        Dictionary<string, int>? stockpile = null)
    {
        var byShip = new Dictionary<string, List<CarrierAirwingAssignment>>();

        foreach (var carrier in ships.Where(ship => ship.Role == ShipRole.Carrier))
        {
            byShip[carrier.ID] = wings ?? [];
        }

        var attacker = new BattleParticipant(new Fleet("a", ships, byShip), null, [], [], landBasedAirwings: landBased, planeStockpile: stockpile);
        var defender = TestEntityFactory.Participant("d", [TestEntityFactory.Screen("enemy", lightAttack: 0)]);
        var scenario = new BattleScenario("air", "ocean", "clear", 1, null, attacker, defender, Planes(), defines, new BattleRules());
        return new AirPhase(scenario, new BattleSide("attacker", attacker), new BattleSide("defender", defender));
    }
}
