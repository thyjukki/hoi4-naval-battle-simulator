namespace NavySimulator.Engine;

/// <summary>
/// A-EXTERNAL: land-based planes of one plane type from one air base, on a naval strike mission over the battle's
/// region. <paramref name="MissionEfficiency"/> is the mission's efficiency (air base, range, coverage; an input), which
/// scales the planes offered to the join cap; <paramref name="DisruptionFactor"/> multiplies their attack (1 unless
/// enemy interceptors disrupt the mission; an input).
/// </summary>
public record LandBasedAirwing(string PlaneID, int Planes, double MissionEfficiency = 1.0, double DisruptionFactor = 1.0);
