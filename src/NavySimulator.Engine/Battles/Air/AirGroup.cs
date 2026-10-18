namespace NavySimulator.Engine.Battles.Air;

/// <summary>
/// [RE] An air object in the battle ("AirFX"): the wings of one carrier, or the land-based planes that joined.
/// </summary>
public sealed class AirGroup
{
    private AirGroup(Ship? carrier, List<AirWing> wings, double missionEfficiency)
    {
        Carrier = carrier;
        Wings = wings;
        MissionEfficiency = missionEfficiency;
    }

    public Ship? Carrier { get; }
    public bool IsExternal => Carrier is null;
    public List<AirWing> Wings { get; }

    /// <summary>[RE] External groups multiply their attack by this efficiency (air mission efficiency, an input).</summary>
    public double MissionEfficiency { get; }

    /// <summary>All planes of all wings, fighters included.</summary>
    public int Planes => Wings.Sum(wing => wing.Planes);

    public IEnumerable<AirWing> StrikeWings => Wings.Where(wing => wing.IsStrikeWing && wing.Planes > 0);
    public IEnumerable<AirWing> FighterWings => Wings.Where(wing => wing.IsFighter && wing.Planes > 0);

    public static AirGroup ForCarrier(Ship carrier, List<AirWing> wings)
    {
        return new AirGroup(carrier, wings, 1.0);
    }

    public static AirGroup External(List<AirWing> wings, double missionEfficiency)
    {
        return new AirGroup(null, wings, missionEfficiency);
    }
}
