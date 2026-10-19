namespace NavySimulator.Engine;

/// <summary>A-WINGS: <paramref name="Airwings"/> wings of <paramref name="PlaneCount"/> planes of one plane type on a carrier.</summary>
public record CarrierAirwingAssignment(string PlaneID, int Airwings, int PlaneCount);
