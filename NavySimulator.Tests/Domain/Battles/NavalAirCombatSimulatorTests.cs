using NavySimulator.Domain;
using NavySimulator.Domain.Battles;
using NavySimulator.Domain.Stats;
using System.Reflection;
using Xunit;

namespace NavySimulator.Tests.Domain.Battles;

public class NavalAirCombatSimulatorTests
{
    [Fact]
    public void CalculateAirSortieSnapshot_UsesBaseSortieEfficiency_WhenNoScreenAndCapitalSupport()
    {
        var simulator = new NavalAirCombatSimulator();
        var carrier = CreateShip("carrier-1", "carrier-design", ShipRole.Carrier);
        var ownFleet = CreateFleet(
            "own",
            [carrier],
            carrier.Design.ID,
            airwings: 8,
            planeId: "bomber-basic");
        var enemyFleet = CreateFleet("enemy", [CreateShip("enemy-screen", "enemy-screen-design", ShipRole.Screen)]);

        var ownParticipant = CreateParticipant(ownFleet);
        var enemyParticipant = CreateParticipant(enemyFleet);
        var scenario = CreateScenario(ownParticipant, enemyParticipant, "bomber-basic");

        var ownLines = BattleLineCalculator.BuildBattleLinesFromFleet(ownFleet.Ships);
        var enemyLines = BattleLineCalculator.BuildBattleLinesFromFleet(enemyFleet.Ships);
        var ownScreening = BattleLineCalculator.CalculateScreening(ownLines, positioning: 1.0);
        var wingStates = simulator.BuildCarrierWingStatesByWingKey(ownFleet);

        var snapshot = simulator.CalculateAirSortieSnapshot(
            scenario,
            ownParticipant,
            ownLines,
            ownScreening,
            enemyLines,
            wingStates,
            hour: 9);

        Assert.True(snapshot.IsSortieHour);
        Assert.Equal(80, snapshot.CarrierAssignedPlanes);
        Assert.Equal(16, snapshot.CarrierSortiePlanes);
    }

    [Fact]
    public void CalculateAirSortieSnapshot_IncreasesSorties_WithScreenAndCapitalSupport()
    {
        var simulator = new NavalAirCombatSimulator();
        var carrier = CreateShip("carrier-1", "carrier-design", ShipRole.Carrier);
        var supportingScreen = CreateShip("screen-1", "screen-design", ShipRole.Screen);
        var supportingCapital = CreateShip("capital-1", "capital-design", ShipRole.Capital);
        var ownFleet = CreateFleet(
            "own",
            [carrier, supportingScreen, supportingCapital],
            carrier.Design.ID,
            airwings: 8,
            planeId: "bomber-basic");
        var enemyFleet = CreateFleet("enemy", [CreateShip("enemy-screen", "enemy-screen-design", ShipRole.Screen)]);

        var ownParticipant = CreateParticipant(ownFleet);
        var enemyParticipant = CreateParticipant(enemyFleet);
        var scenario = CreateScenario(ownParticipant, enemyParticipant, "bomber-basic");

        var ownLines = BattleLineCalculator.BuildBattleLinesFromFleet(ownFleet.Ships);
        var enemyLines = BattleLineCalculator.BuildBattleLinesFromFleet(enemyFleet.Ships);
        var ownScreening = BattleLineCalculator.CalculateScreening(ownLines, positioning: 1.0);
        var wingStates = simulator.BuildCarrierWingStatesByWingKey(ownFleet);

        var snapshot = simulator.CalculateAirSortieSnapshot(
            scenario,
            ownParticipant,
            ownLines,
            ownScreening,
            enemyLines,
            wingStates,
            hour: 9);

        Assert.True(snapshot.IsSortieHour);
        Assert.Equal(0.75, snapshot.CarrierSortieEfficiencyMultiplier, double.Epsilon);
        Assert.Equal(80, snapshot.CarrierAssignedPlanes);
        Assert.Equal(60, snapshot.CarrierSortiePlanes);
    }
    [Fact]
    public void CalculateAirSortieSnapshot_UsesBaseSortieEfficiency_1_should_be_0()
    {
        var simulator = new NavalAirCombatSimulator();
        var carrier = CreateShip("carrier-1", "carrier-design", ShipRole.Carrier);
        var ownFleet = CreateFleet(
            "own",
            [carrier],
            carrier.Design.ID,
            airwings: 1,
            planeCount: 1,
            planeId: "bomber-basic");
        var enemyFleet = CreateFleet("enemy", [CreateShip("enemy-screen", "enemy-screen-design", ShipRole.Screen)]);

        var ownParticipant = CreateParticipant(ownFleet);
        var enemyParticipant = CreateParticipant(enemyFleet);
        var scenario = CreateScenario(ownParticipant, enemyParticipant, "bomber-basic");

        var ownLines = BattleLineCalculator.BuildBattleLinesFromFleet(ownFleet.Ships);
        var enemyLines = BattleLineCalculator.BuildBattleLinesFromFleet(enemyFleet.Ships);
        var ownScreening = BattleLineCalculator.CalculateScreening(ownLines, positioning: 1.0);
        var wingStates = simulator.BuildCarrierWingStatesByWingKey(ownFleet);

        var snapshot = simulator.CalculateAirSortieSnapshot(
            scenario,
            ownParticipant,
            ownLines,
            ownScreening,
            enemyLines,
            wingStates,
            hour: 9);

        Assert.True(snapshot.IsSortieHour);
        Assert.Equal(1, snapshot.CarrierAssignedPlanes);
        Assert.Equal(0, snapshot.CarrierSortiePlanes);
    }
    
    [Fact]
    public void CalculateAirSortieSnapshot_UsesBaseSortieEfficiency_7_should_be_1()
    {
        var simulator = new NavalAirCombatSimulator();
        var carrier = CreateShip("carrier-1", "carrier-design", ShipRole.Carrier);
        var ownFleet = CreateFleet(
            "own",
            [carrier],
            carrier.Design.ID,
            airwings: 1,
            planeCount: 7,
            planeId: "bomber-basic");
        var enemyFleet = CreateFleet("enemy", [CreateShip("enemy-screen", "enemy-screen-design", ShipRole.Screen)]);

        var ownParticipant = CreateParticipant(ownFleet);
        var enemyParticipant = CreateParticipant(enemyFleet);
        var scenario = CreateScenario(ownParticipant, enemyParticipant, "bomber-basic");

        var ownLines = BattleLineCalculator.BuildBattleLinesFromFleet(ownFleet.Ships);
        var enemyLines = BattleLineCalculator.BuildBattleLinesFromFleet(enemyFleet.Ships);
        var ownScreening = BattleLineCalculator.CalculateScreening(ownLines, positioning: 1.0);
        var wingStates = simulator.BuildCarrierWingStatesByWingKey(ownFleet);

        var snapshot = simulator.CalculateAirSortieSnapshot(
            scenario,
            ownParticipant,
            ownLines,
            ownScreening,
            enemyLines,
            wingStates,
            hour: 9);

        Assert.True(snapshot.IsSortieHour);
        Assert.Equal(0.2, snapshot.CarrierSortieEfficiencyMultiplier);
        Assert.Equal(7, snapshot.CarrierAssignedPlanes);
        Assert.Equal(1, snapshot.CarrierSortiePlanes);
    }
    
    [Fact]
    public void CalculateAirSortieSnapshot_UsesBaseSortieEfficiency_10_should_be_2()
    {
        var simulator = new NavalAirCombatSimulator();
        var carrier = CreateShip("carrier-1", "carrier-design", ShipRole.Carrier);
        var ownFleet = CreateFleet(
            "own",
            [carrier],
            carrier.Design.ID,
            airwings: 1,
            planeId: "bomber-basic");
        var enemyFleet = CreateFleet("enemy", [CreateShip("enemy-screen", "enemy-screen-design", ShipRole.Screen)]);

        var ownParticipant = CreateParticipant(ownFleet);
        var enemyParticipant = CreateParticipant(enemyFleet);
        var scenario = CreateScenario(ownParticipant, enemyParticipant, "bomber-basic");

        var ownLines = BattleLineCalculator.BuildBattleLinesFromFleet(ownFleet.Ships);
        var enemyLines = BattleLineCalculator.BuildBattleLinesFromFleet(enemyFleet.Ships);
        var ownScreening = BattleLineCalculator.CalculateScreening(ownLines, positioning: 1.0);
        var wingStates = simulator.BuildCarrierWingStatesByWingKey(ownFleet);

        var snapshot = simulator.CalculateAirSortieSnapshot(
            scenario,
            ownParticipant,
            ownLines,
            ownScreening,
            enemyLines,
            wingStates,
            hour: 9);

        Assert.True(snapshot.IsSortieHour);
        Assert.Equal(0.2, snapshot.CarrierSortieEfficiencyMultiplier);
        Assert.Equal(10, snapshot.CarrierAssignedPlanes);
        Assert.Equal(2, snapshot.CarrierSortiePlanes);
    }

    [Fact]
    public void CalculateNavalStrikeDamageBreakdown_ComputesExpectedFinalReductionAndDamage()
    {
        var target = CreateShip("target", "target-design", ShipRole.Screen);
        var defenderFleet = CreateFleet("defender", [target]);
        var defenderLines = BattleLineCalculator.BuildBattleLinesFromFleet(defenderFleet.Ships);

        const bool isCarrierBased = false;
        const double navalAttack = 100;
        const double navalTargeting = 10;
        const int planesRemaining = 10;

        var method = typeof(NavalAirCombatSimulator).GetMethod(
            "CalculateNavalStrikeDamageBreakdown",
            BindingFlags.NonPublic | BindingFlags.Static);

        Assert.NotNull(method);

        var breakdown = method!.Invoke(null, [isCarrierBased, navalAttack, navalTargeting, planesRemaining, target, defenderLines]);
        var damageBreakdown = Assert.IsType<NavalStrikeDamageBreakdown>(breakdown);

        var expectedHitRatio = Math.Clamp((navalTargeting / 10.0) * Hoi4Defines.NAVAL_STRIKE_TARGETTING_TO_AMOUNT, 0, 1);
        var expectedHitPlanes = Math.Round(planesRemaining * expectedHitRatio);
        var expectedRawDamage = expectedHitPlanes * navalAttack;
        var expectedTargetAa = target.GetFinalStats().AntiAir;
        var expectedFleetAa = expectedTargetAa;
        var expectedAaPool = expectedTargetAa + Hoi4Defines.SHIP_TO_FLEET_ANTI_AIR_RATIO * expectedFleetAa;
        var expectedDamageMultiplier = Math.Clamp(
            1 - Math.Pow(Math.Max(0, expectedAaPool), Hoi4Defines.ANTI_AIR_POW_ON_INCOMING_AIR_DAMAGE) * Hoi4Defines.ANTI_AIR_MULT_ON_INCOMING_AIR_DAMAGE,
            0,
            1);
        var expectedFinalDamage = Math.Max(0, expectedRawDamage * expectedDamageMultiplier);
        var expectedReduction = 1.0 - expectedDamageMultiplier;

        Assert.Equal(expectedFinalDamage, damageBreakdown.FinalDamageBeforeHpClamp, 6);
        Assert.Equal(expectedReduction, damageBreakdown.CombinedFleetAaDamageReduction, 6);
    }

    private static BattleScenario CreateScenario(
        BattleParticipant attacker,
        BattleParticipant defender,
        string bomberPlaneId)
    {
        var planesById = new Dictionary<string, PlaneEquipment>(StringComparer.Ordinal)
        {
            [bomberPlaneId] = new(bomberPlaneId, new PlaneStats(NavalAttack: 10, NavalTargeting: 10, Agility: 10))
        };

        return new BattleScenario(
            id: "test-scenario",
            terrain: "ocean",
            weather: "clear",
            maxHours: 24,
            iterations: 1,
            attacker: attacker,
            defender: defender,
            planesById: planesById,
            continueAfterRetreat: false,
            dontRetreat: false);
    }

    private static BattleParticipant CreateParticipant(Fleet fleet)
    {
        return new BattleParticipant(
            fleet,
            commander: string.Empty,
            doctrine: string.Empty,
            shipExperienceLevelOverride: null,
            externalNavalStrikePlanes: 0,
            researches: [],
            spirits: []);
    }

    private static Fleet CreateFleet(
        string id,
        List<Ship> ships,
        string? carrierDesignId = null,
        int airwings = 0,
        int planeCount = 10,
        string planeId = "")
    {
        var assignments = new Dictionary<string, List<CarrierAirwingAssignment>>(StringComparer.Ordinal);

        if (!string.IsNullOrWhiteSpace(carrierDesignId) && airwings > 0)
        {
            assignments[carrierDesignId] =
            [
                new CarrierAirwingAssignment(planeId, AirwingType.Bomber, airwings, planeCount)
            ];
        }

        return new Fleet(id, ships, assignments);
    }

    private static Ship CreateShip(string shipId, string designId, ShipRole role)
    {
        var hull = new Hull(
            id: $"{designId}-hull",
            role: role,
            types: [role.ToString()],
            manpower: 400,
            baseStats: new ShipStats(
                Speed: 30,
                Organization: 40,
                Hp: 100,
                SurfaceVisibility: 20,
                SubVisibility: 5,
                AntiAir: 5,
                ProductionCost: 1000));
        var design = new ShipDesign(designId, hull, []);
        return new Ship(shipId, design);
    }
}


