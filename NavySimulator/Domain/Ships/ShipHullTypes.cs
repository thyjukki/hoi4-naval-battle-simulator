namespace NavySimulator.Domain;

public static class ShipHullTypes
{
    public const string SHBB = "SHBB";
    public const string Battleship = "Battleship";
    public const string Battlecruiser = "Battlecruiser";
    public const string Carrier = "Carrier";
    public const string HeavyCruiser = "Heavy Cruiser";
    public const string LightCruiser = "Light Cruiser";
    public const string TorpedoCruiser = "Torpedo Cruiser";
    public const string Destroyer = "Destroyer";
    public const string Submarine = "Submarine";

    public static readonly IReadOnlyList<string> All =
    [
        SHBB,
        Battleship,
        Battlecruiser,
        Carrier,
        HeavyCruiser,
        LightCruiser,
        TorpedoCruiser,
        Destroyer,
        Submarine
    ];

    private static readonly HashSet<string> KnownTypes = new(All, StringComparer.OrdinalIgnoreCase);

    public static bool IsKnown(string hullType)
    {
        return KnownTypes.Contains(hullType);
    }
}
