using NavySimulator.Engine.Defines;
using NavySimulator.Engine.Stats;

namespace NavySimulator.Engine.Battles.Mechanics;

/// <summary>
/// S-NIGHT [RE + LOG]: a province's darkness from its map position, the day of the year and the hour of day, in the
/// game's fx5 arithmetic (0xf14fe0, 0xf15eb0, 0xf20c50). Reproduces every logged darkness of the TFB weather battles.
/// Spec: docs/mechanics/surface.md.
/// </summary>
public static class DarknessRules
{
    /// <summary>
    /// [RE] 0xf14fe0: a province's direction for each hour of day, once per province. <c>tz = (x − GMT_OFFSET) / W + 1</c>,
    /// polar angle <c>lat = y / H × (NORTH_POLE_OFFSET − SOUTH_POLE_OFFSET) + SOUTH_POLE_OFFSET</c> (× π), <c>c = −cos</c>,
    /// <c>r = 1 − |c|</c>, then <c>normalize(r sin a, c, r cos a)</c> with <c>a = 2π' × frac(tz + h / 24)</c>. x and y are
    /// whole map pixels, y from the bottom row.
    /// </summary>
    public static (long X, long Y, long Z)[] ProvinceVectors(double x, double y, int mapWidth, int mapHeight, GameDefines defines)
    {
        var graphics = defines.NGraphics;
        var one = GameMath.One;
        var south = GameMath.Define(graphics.SOUTH_POLE_OFFSET);
        var north = GameMath.Define(graphics.NORTH_POLE_OFFSET);
        var timeZone = ((long)x - (long)graphics.GMT_OFFSET) * one * one / (mapWidth * one) + one;
        var height = (long)y * one * one / (mapHeight * one);
        var latitude = GameMath.Multiply(north - south, height) + south;
        var c = -GameMath.Cos(GameMath.Multiply(latitude, GameMath.Pi));
        var r = one - Math.Abs(c);
        var dayAngle = GameMath.Define(defines.Hardcoded.DarknessDayAngle);
        var vectors = new (long X, long Y, long Z)[24];

        for (var hour = 0; hour < 24; hour++)
        {
            var time = timeZone + hour * one * one / (24 * one);
            time -= time / one * one;
            var angle = time * dayAngle / one;
            vectors[hour] = GameMath.Normalize(GameMath.Multiply(r, GameMath.Sin(angle)), c, GameMath.Multiply(GameMath.Cos(angle), r));
        }

        return vectors;
    }

    /// <summary>
    /// [RE] 0xf15eb0: the night direction <c>(0, sin θ, cos θ)</c> of a day of the year (0 = 1 January), θ running linearly
    /// from −5° at the summer solstice to +5° at the winter solstice and back.
    /// </summary>
    public static (long X, long Y, long Z) SunDirection(int dayOfYear, GameDefines defines)
    {
        var hardcoded = defines.Hardcoded;
        var one = GameMath.One;
        long summer = hardcoded.SummerSolsticeDay;
        long winter = hardcoded.WinterSolsticeDay;
        long year = hardcoded.DaysPerYear;
        var tilt = GameMath.Define(hardcoded.SunTiltDegrees);
        var day = (long)dayOfYear;
        long degrees;

        if (day >= summer && day <= winter)
        {
            var t = GameMath.Divide((day - summer) * one, (winter - summer) * one);
            degrees = GameMath.Multiply(2 * tilt, t) - tilt;
        }
        else
        {
            var t = day > winter
                ? GameMath.Divide((day - winter) * one, (summer + year - winter) * one)
                : GameMath.Divide((day - (winter - year)) * one, (summer - (winter - year)) * one);
            degrees = GameMath.Multiply(2 * tilt, one / 2 - t);
        }

        var angle = GameMath.Multiply(degrees * one / (180 * one), GameMath.Pi);
        var cos = Math.Clamp(GameMath.Cos(angle), -one, one);
        var sin = Math.Clamp(GameMath.Sin(angle), -one, one);
        return (0, GameMath.Multiply(sin, one), GameMath.Multiply(one, cos));
    }

    /// <summary>
    /// [RE] 0xf20c50: <c>clamp((n · v[hour] + F / 2) / F, 0, 1)</c> with F = DAY_NIGHT_FEATHER: 1 at night, 0 by day, a
    /// ramp in at most one hour at dusk or dawn.
    /// </summary>
    public static double Darkness((long X, long Y, long Z)[] provinceVectors, int dayOfYear, int hourOfDay, GameDefines defines)
    {
        var one = GameMath.One;
        var sun = SunDirection(dayOfYear, defines);
        var v = provinceVectors[((hourOfDay % 24) + 24) % 24];
        var dot = GameMath.Multiply(sun.Y, v.Y) + GameMath.Multiply(sun.Z, v.Z) + GameMath.Multiply(sun.X, v.X);
        var half = GameMath.Multiply(GameMath.Define(defines.NGraphics.DAY_NIGHT_FEATHER), one / 2);

        if (half == 0)
        {
            return dot > 0 ? 1 : 0;
        }

        var inverse = one * one / (2 * half);
        var value = GameMath.Multiply(dot + half, inverse);
        return Math.Clamp(value, 0, one) / (double)one;
    }
}
