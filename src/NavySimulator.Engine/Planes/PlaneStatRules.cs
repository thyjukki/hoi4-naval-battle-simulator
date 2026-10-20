using NavySimulator.Engine.Stats;

namespace NavySimulator.Engine;

/// <summary>A-STATS: a plane's stats with its country's sub-unit bonuses (doctrines, spirits). Spec: air.md A-STATS.</summary>
public static class PlaneStatRules
{
    /// <summary>
    /// A-STATS-CACHE [RE] 0xf661e0 / 0x142aa30: each stat is trunc((1 + Σ bonus) × equipment stat), as the ships' technology
    /// path (S-STATS-SHIP); reliability is on the sub-unit's additive list too, × (1 + b) + b.
    /// </summary>
    public static PlaneStats WithBonuses(PlaneStats stats, ModifierSet bonus)
    {
        return Mix([(stats, 1)], bonus);
    }

    /// <summary>
    /// A-STATS-CACHE / A-MIXED [RE] 0xf661e0: a wing of several variants: with each variant's plane share w = trunc(planes /
    /// total), stat = Σ trunc(trunc((1 + bonus) × w) × variant stat); reliability also gets + bonus once.
    /// </summary>
    public static PlaneStats Mix(IReadOnlyList<(PlaneStats Stats, int Planes)> variants, ModifierSet bonus)
    {
        var total = variants.Sum(variant => variant.Planes);

        if (variants.Count == 0)
        {
            return new PlaneStats();
        }

        if (total <= 0)
        {
            variants = [(variants[0].Stats, 1)];
            total = 1;
        }

        double air = 0, defence = 0, agility = 0, speed = 0, naval = 0, targeting = 0, range = 0, reliability = 0;

        foreach (var (stats, planes) in variants.Where(variant => variant.Planes > 0))
        {
            var share = FixedPoint.Truncate((double)planes / total);
            double Part(double value, string key) => value == 0 ? 0 : FixedPoint.Truncate(FixedPoint.Truncate((1 + bonus.Get(key)) * share) * value);

            air += Part(stats.AirAttack, "air_attack");
            defence += Part(stats.AirDefense, "air_defence");
            agility += Part(stats.Agility, "air_agility");
            speed += Part(stats.Speed, "maximum_speed");
            naval += Part(stats.NavalAttack, "naval_strike_attack");
            targeting += Part(stats.NavalTargeting, "naval_strike_targetting");
            range += Part(stats.Range, "air_range");
            reliability += Part(stats.Reliability, "reliability");
        }

        var first = variants[0].Stats;
        return first with
        {
            AirAttack = air,
            AirDefense = defence,
            Agility = agility,
            Speed = speed,
            NavalAttack = naval,
            NavalTargeting = targeting,
            Range = range,
            Reliability = Math.Max(0, reliability + bonus.Get("reliability"))
        };
    }
}
