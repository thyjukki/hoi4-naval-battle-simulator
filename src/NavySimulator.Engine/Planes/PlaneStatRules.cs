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
        if (bonus.Values.Count == 0)
        {
            return stats;
        }

        double Scaled(double value, string key) => value == 0 ? 0 : FixedPoint.Truncate((1 + bonus.Get(key)) * value);

        return stats with
        {
            AirAttack = Scaled(stats.AirAttack, "air_attack"),
            AirDefense = Scaled(stats.AirDefense, "air_defence"),
            Agility = Scaled(stats.Agility, "air_agility"),
            Speed = Scaled(stats.Speed, "maximum_speed"),
            NavalAttack = Scaled(stats.NavalAttack, "naval_strike_attack"),
            NavalTargeting = Scaled(stats.NavalTargeting, "naval_strike_targetting"),
            Range = Scaled(stats.Range, "air_range"),
            Reliability = Math.Max(0, Scaled(stats.Reliability, "reliability") + bonus.Get("reliability"))
        };
    }
}
