using NavySimulator.GameData.Contracts;

namespace NavySimulator.GameData.Import;

/// <summary>The game's equipment stat keys and the <see cref="ShipStatsDto"/> stats they map to.</summary>
public static class ShipStatKeys
{
    public static readonly IReadOnlyDictionary<string, string> ByGameKey = new Dictionary<string, string>(StringComparer.Ordinal)
    {
        ["lg_attack"] = nameof(ShipStatsDto.LightAttack),
        ["lg_armor_piercing"] = nameof(ShipStatsDto.LightPiercing),
        ["hg_attack"] = nameof(ShipStatsDto.HeavyAttack),
        ["hg_armor_piercing"] = nameof(ShipStatsDto.HeavyPiercing),
        ["torpedo_attack"] = nameof(ShipStatsDto.TorpedoAttack),
        ["sub_attack"] = nameof(ShipStatsDto.DepthChargeAttack),
        ["anti_air_attack"] = nameof(ShipStatsDto.AntiAir),
        ["armor_value"] = nameof(ShipStatsDto.Armor),
        ["max_strength"] = nameof(ShipStatsDto.HP),
        ["naval_speed"] = nameof(ShipStatsDto.Speed),
        ["naval_range"] = nameof(ShipStatsDto.NavalRange),
        ["reliability"] = nameof(ShipStatsDto.Reliability),
        ["build_cost_ic"] = nameof(ShipStatsDto.ProductionCost),
        ["surface_visibility"] = nameof(ShipStatsDto.SurfaceVisibility),
        ["sub_visibility"] = nameof(ShipStatsDto.SubVisibility),
        ["surface_detection"] = nameof(ShipStatsDto.SurfaceDetection),
        ["sub_detection"] = nameof(ShipStatsDto.SubDetection),
        ["carrier_surface_detection"] = nameof(ShipStatsDto.CarrierSurfaceDetection),
        ["carrier_sub_detection"] = nameof(ShipStatsDto.CarrierSubDetection),
        ["carrier_size"] = nameof(ShipStatsDto.CarrierSize),
        ["naval_torpedo_damage_reduction_factor"] = nameof(ShipStatsDto.TorpedoDamageReductionFactor),
        ["naval_torpedo_enemy_critical_chance_factor"] = nameof(ShipStatsDto.TorpedoEnemyCriticalChanceFactor),
        ["naval_torpedo_hit_chance_factor"] = nameof(ShipStatsDto.TorpedoHitChanceFactor),
        ["naval_light_gun_hit_chance_factor"] = nameof(ShipStatsDto.LightHitChanceFactor),
        ["naval_heavy_gun_hit_chance_factor"] = nameof(ShipStatsDto.HeavyHitChanceFactor),
        ["naval_weather_penalty_factor"] = nameof(ShipStatsDto.NavalWeatherPenaltyFactor),

        // A sub-unit stat (S-STATS-SHIP): technology and doctrine sub-unit bonuses set it; no ship equipment or module does.
        ["max_organisation"] = nameof(ShipStatsDto.Organization)
    };

    /// <summary>Adds <paramref name="value"/> to the stat a game key maps to. Returns false for keys the stat model has no stat for.</summary>
    public static bool TryAdd(ShipStatsDto stats, string gameKey, double value)
    {
        if (!ByGameKey.TryGetValue(gameKey, out var property))
        {
            return false;
        }

        var info = typeof(ShipStatsDto).GetProperty(property)!;
        info.SetValue(stats, (double)info.GetValue(stats)! + value);
        return true;
    }

    /// <summary>Sets the stat a game key maps to. Returns false for keys the stat model has no stat for.</summary>
    public static bool TrySet(ShipStatsDto stats, string gameKey, double value)
    {
        if (!ByGameKey.TryGetValue(gameKey, out var property))
        {
            return false;
        }

        typeof(ShipStatsDto).GetProperty(property)!.SetValue(stats, value);
        return true;
    }
}
