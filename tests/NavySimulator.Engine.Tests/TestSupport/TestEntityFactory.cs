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
        SubDetection: 1,
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

    public static ShipStats SubmarineStats() => new(
        Speed: 15,
        Organization: 30,
        Hp: 50,
        SubVisibility: 10,
        Reliability: 0.8,
        TorpedoAttack: 20,
        Armor: 0);

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

    public static Ship Carrier(string id)
    {
        return CreateShip(id, ShipRole.Carrier, CapitalStats() with { HeavyAttack = 0 });
    }

    public static Ship Submarine(string id)
    {
        return CreateShip(id, ShipRole.Submarine, SubmarineStats());
    }

    public static BattleParticipant Participant(string fleetId, IEnumerable<Ship> ships, Aggressiveness aggressiveness = Aggressiveness.EngageAtMediumRisk)
    {
        return new BattleParticipant(new Fleet(fleetId, ships.ToList()), string.Empty, string.Empty, 0, [], [], aggressiveness);
    }

    public static BattleScenario Scenario(
        IEnumerable<Ship> attackerShips,
        IEnumerable<Ship> defenderShips,
        BattleRules? rules = null,
        GameDefines? defines = null,
        Aggressiveness attackerAggressiveness = Aggressiveness.EngageAtMediumRisk)
    {
        return new BattleScenario(
            "test",
            "ocean",
            "clear",
            1,
            null,
            Participant("attacker_fleet", attackerShips, attackerAggressiveness),
            Participant("defender_fleet", defenderShips),
            new Dictionary<string, PlaneEquipment>(),
            defines ?? GameDefines.Vanilla(),
            rules ?? new BattleRules());
    }

    /// <summary>Two sides facing each other, with the shooter side's enemy snapshot taken as the engine would.</summary>
    public static (BattleSide Own, BattleSide Enemy) Sides(
        IEnumerable<Ship> ownShips,
        IEnumerable<Ship> enemyShips,
        Aggressiveness ownAggressiveness = Aggressiveness.EngageAtMediumRisk)
    {
        var own = new BattleSide("own", Participant("own", ownShips, ownAggressiveness));
        var enemy = new BattleSide("enemy", Participant("enemy", enemyShips));
        own.EnemyCounts = EnemyLineCounts.Of(enemy.Ships);
        enemy.EnemyCounts = EnemyLineCounts.Of(own.Ships);
        return (own, enemy);
    }
}
