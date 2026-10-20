using NavySimulator.Engine.Defines;
using NavySimulator.Engine.Environment;
using NavySimulator.Engine.Stats;

namespace NavySimulator.Engine.Battles.Air;

/// <summary>
/// A-COVERAGE [RE]: a land-based wing's mission effectiveness from its air base (0xf78500 land branch): the coverage of the
/// battle's region from the base, the base's crowding and supply. Spec: air.md A-COVERAGE; finding
/// A-COVERAGE-land-mission-effectiveness.md.
/// </summary>
public static class AirCoverageRules
{
    /// <summary>
    /// [RE] 0xf5e5c0: the wing's range in whole map pixels, trunc(trunc(air_range / MAP_SCALE_PIXEL_TO_KM) × (1 +
    /// air_range_factor)) with the first quotient in fixed point.
    /// </summary>
    public static int RangePixels(double rangeKm, double rangeFactor, GameDefines defines)
    {
        var scale = defines.NGame.MAP_SCALE_PIXEL_TO_KM;

        if (scale <= 0)
        {
            return 0;
        }

        return (int)Math.Truncate(FixedPoint.Truncate(FixedPoint.Truncate(rangeKm / scale) * (1 + rangeFactor)));
    }

    /// <summary>
    /// [RE] 0xf87480 → 0x10132d0 → 0x1012f80: min(1, trunc(provinces in range / the region's provinces)). A province is in
    /// range when dx² + dy² ≤ R² from the base (dx wrapped around the map's width). The region is skipped (0) unless the
    /// base is within R² + (W/2)² + (H/2)² of the centre of the region's box, and when the box has no height (a region
    /// without an imported box skips the precheck).
    /// </summary>
    public static double Coverage(AirBase airBase, SeaRegion region, int rangePixels)
    {
        if (region.Provinces.Count == 0)
        {
            return 0;
        }

        long r2 = (long)rangePixels * rangePixels;

        if (region.BoxWidth > 0)
        {
            var halfWidth = region.BoxWidth / 2;
            var halfHeight = region.BoxHeight / 2;

            if (region.BoxHeight <= 0 ||
                DistanceSquared(airBase.X, airBase.Y, region.BoxX + halfWidth, region.BoxY + halfHeight, region.MapWidth) >
                r2 + (long)halfWidth * halfWidth + (long)halfHeight * halfHeight)
            {
                return 0;
            }
        }

        var inRange = region.Provinces.Count(province =>
            DistanceSquared(airBase.X, airBase.Y, (int)province.X, (int)province.Y, region.MapWidth) <= r2);
        return Math.Min(1, FixedPoint.Truncate((double)inRange / region.Provinces.Count));
    }

    /// <summary>
    /// [RE] 0xc579f0 / 0xc57b70: the base's crowding: 1 while its planes are within its capacity (air base level ×
    /// AIRBASE_CAPACITY_MULT), else with r = trunc((planes − capacity) / capacity), 0 above 0.9, otherwise
    /// clamp(1 − trunc(CAPACITY_PENALTY × r), 0, 1).
    /// </summary>
    public static double Crowding(int planes, int level, GameDefines defines)
    {
        var capacity = level * defines.NBuildings.AIRBASE_CAPACITY_MULT;

        if (planes <= capacity)
        {
            return 1;
        }

        if (capacity <= 0)
        {
            return 0;
        }

        var overfill = FixedPoint.Truncate((planes - capacity) / capacity);
        return overfill > 0.9 ? 0 : Math.Clamp(1 - FixedPoint.Truncate(defines.NAir.CAPACITY_PENALTY * overfill), 0, 1);
    }

    // [RE] 0x9d37a0 / 0x118f660: squared distance with dx wrapped into [−W/2, W/2]; no wrap in y.
    private static long DistanceSquared(int x0, int y0, int x1, int y1, int mapWidth)
    {
        long dx = x1 - x0;

        if (mapWidth > 0)
        {
            if (dx > mapWidth / 2)
            {
                dx -= mapWidth;
            }
            else if (dx < -(mapWidth / 2))
            {
                dx += mapWidth;
            }
        }

        long dy = y1 - y0;
        return dx * dx + dy * dy;
    }
}
