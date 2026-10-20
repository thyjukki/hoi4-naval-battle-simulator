namespace NavySimulator.Engine.Environment;

/// <summary>
/// S-REGION: where and when a battle is fought: the strategic region (its weather periods), the battle province (its
/// darkness, S-NIGHT), the naval terrain (S-TERRAIN; the region's, or a scenario override) and the start date (the day;
/// the hour is <c>rules.startHourOfDay</c>). Any part may be missing: no province or date means the night window of the
/// rules, no terrain means none.
/// </summary>
public sealed class BattleLocation(SeaRegion? region, SeaProvince? province, NavalTerrain? terrain, GameDate? startDate)
{
    public static BattleLocation Unknown { get; } = new(null, null, null, null);

    public SeaRegion? Region { get; } = region;

    public SeaProvince? Province { get; } = province;

    public NavalTerrain? Terrain { get; } = terrain;

    public GameDate? StartDate { get; } = startDate;

    /// <summary>Whether darkness can be computed from the terminator: a province on a known map and a start date.</summary>
    public bool HasTerminator => Province is not null && StartDate is not null && Region is { MapWidth: > 0, MapHeight: > 0 };
}
