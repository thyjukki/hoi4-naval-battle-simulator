using NavySimulator.Engine;
using NavySimulator.Engine.Battles;
using NavySimulator.Engine.Defines;
using NavySimulator.Engine.Stats;

namespace NavySimulator.Engine.Tests.TestSupport;

internal static class TestEntityFactory
{
    public static ShipStats ScreenStats(double lightAttack = 5, double torpedoAttack = 0) => new(
        Speed: 30,
        Organization: 40,
        Hp: 100,
        SurfaceVisibility: 15,
        SurfaceDetection: 20,
        Reliability: 0.8,
        LightAttack: lightAttack,
        LightPiercing: 5,
        TorpedoAttack: torpedoAttack,
        Armor: 2);

    public static ShipStats CapitalStats() => new(
        Speed: 25,
        Organization: 40,
        Hp: 500,
        SurfaceVisibility: 40,
        SurfaceDetection: 20,
        Reliability: 0.8,
        LightAttack: 5,
        LightPiercing: 5,
        HeavyAttack: 40,
        HeavyPiercing: 60,
        Armor: 50);

    public static Ship CreateShip(string id, ShipRole role, ShipStats stats, double experience = 0, int manpower = 400)
    {
        var hull = new Hull($"{id}_hull", role, [role.ToString()], manpower, stats);
        var design = new ShipDesign($"{id}_design", hull, []);
        return new Ship(id, design, experience);
    }

    public static Ship Screen(string id, double lightAttack = 5, double torpedoAttack = 0)
    {
        return CreateShip(id, ShipRole.Screen, ScreenStats(lightAttack, torpedoAttack));
    }

    public static Ship Capital(string id)
    {
        return CreateShip(id, ShipRole.Capital, CapitalStats());
    }

    public static BattleParticipant Participant(string fleetId, IEnumerable<Ship> ships, RetreatPriority priority = RetreatPriority.Medium)
    {
        return new BattleParticipant(new Fleet(fleetId, ships.ToList()), string.Empty, string.Empty, 0, [], [], priority);
    }

    public static BattleScenario Scenario(
        IEnumerable<Ship> attackerShips,
        IEnumerable<Ship> defenderShips,
        BattleRules? rules = null,
        GameDefines? defines = null)
    {
        return new BattleScenario(
            "test",
            "ocean",
            "clear",
            1,
            null,
            Participant("attacker_fleet", attackerShips),
            Participant("defender_fleet", defenderShips),
            new Dictionary<string, PlaneEquipment>(),
            defines ?? GameDefines.Vanilla(),
            rules ?? new BattleRules());
    }

    public static BattleSide Side(string label, params Ship[] ships)
    {
        var side = new BattleSide(label, Participant(label, ships));
        side.Screening = new ScreeningSummary(1, 1);
        return side;
    }
}
