namespace NavySimulator.Engine;

/// <summary>S-CRIT-PART: a part a design's ships can have damaged, and how many times (-1: any).</summary>
public sealed record CriticalPartCandidate(CriticalPart Part, int MaxInstances);
