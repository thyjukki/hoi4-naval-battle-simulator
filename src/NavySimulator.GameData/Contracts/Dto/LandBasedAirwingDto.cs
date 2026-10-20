namespace NavySimulator.GameData.Contracts;

/// <summary>A-EXTERNAL: land-based planes of one type from one air base that join the battle.</summary>
public class LandBasedAirwingDto
{
    public string PlaneID { get; set; } = string.Empty;
    public int Planes { get; set; }

    /// <summary>
    /// The air mission's efficiency; scales the planes offered to the battle's join cap. Null: 1. With <see cref="AirBase"/>
    /// it is only the factors the simulator does not compute (region change penalty, fuel); coverage, crowding and supply
    /// come from the base (A-COVERAGE).
    /// </summary>
    public double? MissionEfficiency { get; set; }

    /// <summary>A-COVERAGE: the state whose air base the wing flies from (<c>air-bases/</c>); null: no base, coverage 1.</summary>
    public int? AirBase { get; set; }

    /// <summary>The mission's disruption factor (1 without enemy interceptors); multiplies their attack. Null: 1.</summary>
    public double? DisruptionFactor { get; set; }

    /// <summary>
    /// The game's mission keyword over the battle's region: <c>naval_bomber</c> (naval strike; null means this) or
    /// <c>air_superiority</c> (fights in the region's air war, does not join the naval battle; A-WINGS-EXT).
    /// </summary>
    public string? Mission { get; set; }
}
