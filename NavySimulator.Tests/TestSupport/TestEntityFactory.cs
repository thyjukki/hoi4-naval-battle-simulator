using NavySimulator.Domain;
using NavySimulator.Domain.Stats;

namespace NavySimulator.Tests.TestSupport;

internal static class TestEntityFactory
{
    public static Ship CreateShip(string shipId, string designId)
    {
        return CreateShip(
            shipId,
            designId,
            ShipRole.Screen,
            2,
            new ShipStats(
                Speed: 30,
                Organization: 50,
                Hp: 100,
                LightAttack: 1,
                LightPiercing: 1,
                SurfaceVisibility: 20,
                SubVisibility: 5,
                AntiAir: 5,
                ProductionCost: 1000));
    }

    public static Ship CreateShip(string shipId, string designId, ShipRole role)
    {
        return CreateShip(
            shipId,
            designId,
            role,
            2,
            new ShipStats(
                Speed: 30,
                Organization: 40,
                Hp: 100,
                SurfaceVisibility: 20,
                SubVisibility: 5,
                AntiAir: 5,
                ProductionCost: 1000));
    }

    public static Ship CreateShip(string shipId, string designId, ShipStats baseStats)
    {
        return CreateShip(shipId, designId, ShipRole.Screen, 2, baseStats);
    }

    public static Ship CreateShip(string shipId, string designId, ShipRole role, int experienceLevel, ShipStats baseStats)
    {
        var hull = new Hull(
            id: $"{designId}-hull",
            role: role,
            types: [role.ToString()],
            manpower: 400,
            baseStats: baseStats);

        var design = new ShipDesign(designId, hull, []);
        return new Ship(shipId, design, experienceLevel);
    }

    public static Ship CreateShip(string shipId, string designId, ShipRole role, int experienceLevel, ShipStats baseStats, int manpower)
    {
        var hull = new Hull(
            id: $"{designId}-hull",
            role: role,
            types: [role.ToString()],
            manpower: manpower,
            baseStats: baseStats);

        var design = new ShipDesign(designId, hull, []);
        return new Ship(shipId, design, experienceLevel);
    }
}

