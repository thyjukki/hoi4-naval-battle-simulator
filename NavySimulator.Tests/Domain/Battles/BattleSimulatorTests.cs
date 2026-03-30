using NavySimulator.Domain;
using NavySimulator.Domain.Battles;
using NavySimulator.Domain.Stats;
using Xunit;

namespace NavySimulator.Tests.Domain.Battles;

public class BattleSimulatorTests
{
    [Fact]
    public void Test()
    {
        var attackerShip = CreateShip("attacker_dd_001", "attacker_dd");
        var defenderShip = CreateShip("defender_dd_001", "defender_dd");

        var attackerFleet = new Fleet("attacker_fleet", [attackerShip], new Dictionary<string, List<CarrierAirwingAssignment>>(StringComparer.Ordinal));
        var defenderFleet = new Fleet("defender_fleet", [defenderShip], new Dictionary<string, List<CarrierAirwingAssignment>>(StringComparer.Ordinal));

        var scenario = new BattleScenario(
            id: "test-scenario",
            terrain: "ocean",
            weather: "clear",
            maxHours: 1,
            iterations: 1,
            seed: 42,
            attacker: new BattleParticipant(attackerFleet, "", "", null, 0, [], []),
            defender: new BattleParticipant(defenderFleet, "", "", null, 0, [], []),
            planesById: new Dictionary<string, PlaneEquipment>(StringComparer.Ordinal),
            continueAfterRetreat: false,
            dontRetreat: false);

        var simulator = new BattleSimulator();
        var result = simulator.Simulate(scenario);

        Assert.NotNull(result);
        Assert.Equal("test-scenario", result.ScenarioId);
        Assert.Equal(1, result.HoursElapsed);
    }

    [Fact]
    public void Simulate_DoesNotShootAlreadySunkShipAgainInSameHour()
    {
        var attackerShipOne = CreateShip(
            "attacker_dd_001",
            "attacker_dd",
            new ShipStats(
                Speed: 30,
                Organization: 100,
                Hp: 500,
                LightAttack: 200,
                LightPiercing: 200,
                SurfaceVisibility: 30,
                SubVisibility: 5,
                LightHitChanceFactor: 100,
                ProductionCost: 2000));
        var attackerShipTwo = CreateShip(
            "attacker_dd_002",
            "attacker_dd",
            new ShipStats(
                Speed: 30,
                Organization: 100,
                Hp: 500,
                LightAttack: 200,
                LightPiercing: 200,
                SurfaceVisibility: 30,
                SubVisibility: 5,
                LightHitChanceFactor: 100,
                ProductionCost: 2000));
        var defenderShip = CreateShip(
            "defender_dd_001",
            "defender_dd",
            new ShipStats(
                Speed: 1,
                Organization: 10,
                Hp: 1,
                SurfaceVisibility: 1000,
                SubVisibility: 10,
                ProductionCost: 100));

        var attackerFleet = new Fleet("attacker_fleet", [attackerShipOne, attackerShipTwo], new Dictionary<string, List<CarrierAirwingAssignment>>(StringComparer.Ordinal));
        var defenderFleet = new Fleet("defender_fleet", [defenderShip], new Dictionary<string, List<CarrierAirwingAssignment>>(StringComparer.Ordinal));

        var scenario = new BattleScenario(
            id: "single-target-overkill-check",
            terrain: "ocean",
            weather: "clear",
            maxHours: 30,
            iterations: 1,
            seed: 42,
            attacker: new BattleParticipant(attackerFleet, "", "", null, 0, [], []),
            defender: new BattleParticipant(defenderFleet, "", "", null, 0, [], []),
            planesById: new Dictionary<string, PlaneEquipment>(StringComparer.Ordinal),
            continueAfterRetreat: false,
            dontRetreat: true);

        var simulator = new BattleSimulator();
        var result = simulator.Simulate(scenario);

        Assert.Equal(0, result.DefenderShipsRemaining);

        var hpDamageEventsOnDefender = result.ShipReports
            .Where(report => report.Side == "Attacker")
            .SelectMany(report => report.DamagedShips)
            .Where(damageEvent => damageEvent.TargetShipID == defenderShip.ID && damageEvent.AppliedHpDamage > 0)
            .ToList();

        Assert.Single(hpDamageEventsOnDefender);
    }

    private static Ship CreateShip(string shipId, string designId)
    {
        return CreateShip(
            shipId,
            designId,
            new ShipStats(
                Speed: 30,
                Organization: 50,
                Hp: 100,
                LightAttack: 1,
                LightPiercing: 1,
                SurfaceVisibility: 20,
                SubVisibility: 5,
                ProductionCost: 1000));
    }

    private static Ship CreateShip(string shipId, string designId, ShipStats baseStats)
    {
        var hull = new Hull(
            id: $"{designId}-hull",
            role: ShipRole.Screen,
            types: ["Destroyer"],
            manpower: 400,
            baseStats: baseStats);

        var design = new ShipDesign(designId, hull, []);
        return new Ship(shipId, design);
    }
}



