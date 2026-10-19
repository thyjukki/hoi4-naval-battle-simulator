namespace NavySimulator.Engine.Battles.Air;

/// <summary>
/// [RE] An air object in the battle ("AirFX"): one wing pool (the wings of one plane sub-unit) on a carrier, or the
/// land-based planes that joined from one air base (A-WINGS-STRIKE, A-WINGS-EXT).
/// </summary>
public sealed class AirGroup
{
    private AirGroup(Ship? carrier, List<AirWing> wings, string pool, double disruptionFactor, LandBasedAirwing? landBased, string source)
    {
        Carrier = carrier;
        Wings = wings;
        Pool = pool;
        DisruptionFactor = disruptionFactor;
        LandBased = landBased;
        Source = source;
    }

    public Ship? Carrier { get; }
    public bool IsExternal => Carrier is null;
    public List<AirWing> Wings { get; }

    /// <summary>The wing pool: the plane sub-unit (e.g. <c>cv_nav_bomber</c>), or the plane when its sub-unit is unknown.</summary>
    public string Pool { get; }

    /// <summary>[RE] External groups multiply their attack by this factor (airfx+0xc0, the mission's disruption; an input).</summary>
    public double DisruptionFactor { get; }

    /// <summary>A-EXTERNAL: the land-based planes this group stands for; null for a carrier group.</summary>
    public LandBasedAirwing? LandBased { get; }

    /// <summary>A-WINGS-EXT: a land-based group strikes once its planes have joined; carrier groups are always in.</summary>
    public bool HasJoined { get; set; }

    /// <summary>[RE] A-WINGS-EXT airfx+0x58: planes offered at the last join; an external wing loses at most this many.</summary>
    public int Offered { get; set; }

    /// <summary>The strike records' source: the carrier's ID, or "land-based" with the plane type.</summary>
    public string Source { get; }

    /// <summary>All planes of the pool's wings.</summary>
    public int Planes => Wings.Sum(wing => wing.Planes);

    public IEnumerable<AirWing> StrikeWings => Wings.Where(wing => wing.IsStrikeWing && wing.Planes > 0);
    public IEnumerable<AirWing> FighterWings => Wings.Where(wing => wing.IsFighter && wing.Planes > 0);

    public static AirGroup ForCarrier(Ship carrier, List<AirWing> wings, string? pool = null)
    {
        return new AirGroup(carrier, wings, pool ?? wings.FirstOrDefault()?.Plane.Pool ?? string.Empty, 1.0, null, carrier.ID) { HasJoined = true };
    }

    public static AirGroup External(List<AirWing> wings, double disruptionFactor, LandBasedAirwing? landBased = null)
    {
        return new AirGroup(null, wings, wings.FirstOrDefault()?.Plane.Pool ?? string.Empty, disruptionFactor, landBased,
            landBased is null ? "land-based" : $"land-based {landBased.PlaneID}") { HasJoined = landBased is null };
    }
}
