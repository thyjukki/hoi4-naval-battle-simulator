namespace NavySimulator.Engine.Battles.Air;

/// <summary>
/// A-STATS-GETTERS: a wing's mission over the battle's region: whether the wing's base is in the region (a carrier in
/// its task force's region always is, a land base never is for a sea region) and the country modifier of its mission
/// efficiency M (A-STATS-MISSION-EFF: air_superiority_efficiency, air_nav_efficiency, ...; null: none).
/// </summary>
public sealed record RegionMission(bool Home, string? EfficiencyKey);
