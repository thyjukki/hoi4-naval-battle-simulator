using NavySimulator.Engine.Stats;
using NavySimulator.GameData.Contracts;

namespace NavySimulator.Web.Services;

/// <summary>Display metadata for one ship stat: label, value, format, and which direction is better.</summary>
public sealed record StatInfo(string Key, string Label, string Group, Func<ShipStats, double> Get, string Format, bool HigherIsBetter = true, string? ShortLabel = null)
{
    /// <summary>Column header for compact tables.</summary>
    public string Short => ShortLabel ?? Label;

    public static readonly IReadOnlyList<StatInfo> All =
    [
        new("hp", "HP", "Durability", stats => stats.Hp, "F1"),
        new("organization", "Organization", "Durability", stats => stats.Organization, "F1", true, "Org"),
        new("armor", "Armor", "Durability", stats => stats.Armor, "F2"),
        new("reliability", "Reliability", "Durability", stats => stats.Reliability, "P1"),
        new("torpedoDamageReductionFactor", "Torpedo damage reduction", "Durability", stats => stats.TorpedoDamageReductionFactor, "P1"),
        new("torpedoEnemyCriticalChanceFactor", "Enemy torpedo crit chance", "Durability", stats => stats.TorpedoEnemyCriticalChanceFactor, "P1", false),
        new("lightAttack", "Light attack", "Attack", stats => stats.LightAttack, "F2", true, "Light atk"),
        new("lightPiercing", "Light piercing", "Attack", stats => stats.LightPiercing, "F2"),
        new("heavyAttack", "Heavy attack", "Attack", stats => stats.HeavyAttack, "F2", true, "Heavy atk"),
        new("heavyPiercing", "Heavy piercing", "Attack", stats => stats.HeavyPiercing, "F2"),
        new("torpedoAttack", "Torpedo attack", "Attack", stats => stats.TorpedoAttack, "F2", true, "Torpedo"),
        new("depthChargeAttack", "Depth charges", "Attack", stats => stats.DepthChargeAttack, "F2"),
        new("antiAir", "Anti-air", "Attack", stats => stats.AntiAir, "F2", true, "AA"),
        new("lightHitChanceFactor", "Light hit chance", "Hit chance", stats => stats.LightHitChanceFactor, "P1"),
        new("heavyHitChanceFactor", "Heavy hit chance", "Hit chance", stats => stats.HeavyHitChanceFactor, "P1"),
        new("torpedoHitChanceFactor", "Torpedo hit chance", "Hit chance", stats => stats.TorpedoHitChanceFactor, "P1"),
        new("depthChargeHitChanceFactor", "Depth charge hit chance", "Hit chance", stats => stats.DepthChargeHitChanceFactor, "P1"),
        new("speed", "Speed", "Movement and sensors", stats => stats.Speed, "F2"),
        new("surfaceVisibility", "Surface visibility", "Movement and sensors", stats => stats.SurfaceVisibility, "F2", false, "Visibility"),
        new("subVisibility", "Sub visibility", "Movement and sensors", stats => stats.SubVisibility, "F2", false),
        new("surfaceDetection", "Surface detection", "Movement and sensors", stats => stats.SurfaceDetection, "F2"),
        new("subDetection", "Sub detection", "Movement and sensors", stats => stats.SubDetection, "F2"),
        new("carrierSurfaceDetection", "Carrier surface detection", "Movement and sensors", stats => stats.CarrierSurfaceDetection, "F2"),
        new("carrierSubDetection", "Carrier sub detection", "Movement and sensors", stats => stats.CarrierSubDetection, "F2"),
        new("carrierSize", "Carrier size (decks)", "Movement and sensors", stats => stats.CarrierSize, "F0"),
        new("navalWeatherPenaltyFactor", "Weather penalty factor", "Movement and sensors", stats => stats.NavalWeatherPenaltyFactor, "F2", false),
        new("productionCost", "Production cost", "Cost", stats => stats.ProductionCost, "F1", false, "Cost")
    ];

    /// <summary>The headline stats shown in compact tables.</summary>
    public static readonly IReadOnlyList<StatInfo> Headline =
        All.Where(stat => stat.Key is "hp" or "organization" or "armor" or "lightAttack" or "heavyAttack" or "torpedoAttack" or "antiAir" or "speed"
            or "surfaceVisibility" or "productionCost").ToList();

    public string Display(ShipStats stats)
    {
        return Display(Get(stats));
    }

    public string Display(double value)
    {
        return value.ToString(Format);
    }

    /// <summary>A flat change of this stat, e.g. "+20" or, for percentage stats, "+10%".</summary>
    public string DisplayChange(double value)
    {
        return Format.StartsWith('P') ? $"{value * 100:+0.##;-0.##}%" : value.ToString("+0.###;-0.###");
    }

    /// <summary>Non-zero values of a JSON stat block, for module and research summaries.</summary>
    public static IEnumerable<(StatInfo Stat, double Value)> NonZero(ShipStatsDto block)
    {
        var stats = block.ToDomain();

        foreach (var stat in All)
        {
            var value = stat.Get(stats);

            if (value != 0)
            {
                yield return (stat, value);
            }
        }
    }

    /// <summary>A one-line summary of a module or research, e.g. "Heavy attack +20, Heavy piercing ~44, Speed -6%".</summary>
    public static string Summarize(ShipStatsDto modifiers, ShipStatsDto averages, ShipStatsDto multipliers)
    {
        var parts = NonZero(modifiers).Select(entry => $"{entry.Stat.Label} {entry.Stat.DisplayChange(entry.Value)}")
            .Concat(NonZero(averages).Select(entry => $"{entry.Stat.Label} ~{entry.Stat.DisplayChange(entry.Value).TrimStart('+')}"))
            .Concat(NonZero(multipliers).Select(entry => $"{entry.Stat.Label} {entry.Value * 100:+0.#;-0.#}%"));
        return string.Join(", ", parts);
    }
}
