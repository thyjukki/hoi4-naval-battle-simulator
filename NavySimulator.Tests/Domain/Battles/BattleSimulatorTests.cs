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

    private static Ship CreateShip(string shipId, string designId)
    {
        var hull = new Hull(
            id: $"{designId}-hull",
            role: ShipRole.Screen,
            types: ["Destroyer"],
            manpower: 400,
            baseStats: new ShipStats(
                Speed: 30,
                Organization: 50,
                Hp: 100,
                LightAttack: 1,
                LightPiercing: 1,
                SurfaceVisibility: 20,
                SubVisibility: 5,
                ProductionCost: 1000));

        var design = new ShipDesign(designId, hull, []);
        return new Ship(shipId, design);
    }
}



