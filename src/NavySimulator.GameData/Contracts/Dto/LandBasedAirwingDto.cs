namespace NavySimulator.GameData.Contracts;

/// <summary>A-EXTERNAL: land-based planes of one type from one air base that join the battle.</summary>
public class LandBasedAirwingDto
{
    public string PlaneID { get; set; } = string.Empty;
    public int Planes { get; set; }

    /// <summary>The air mission's efficiency; scales the planes offered to the battle's join cap. Null: 1.</summary>
    public double? MissionEfficiency { get; set; }

    /// <summary>The mission's disruption factor (1 without enemy interceptors); multiplies their attack. Null: 1.</summary>
    public double? DisruptionFactor { get; set; }
}
