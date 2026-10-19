namespace NavySimulator.Engine.Defines;

/// <summary>
/// Values from <c>common/modifiers/00_static_modifiers.txt</c> that naval combat applies directly, keyed by block and
/// modifier name (e.g. <c>screening_bonus</c> / <c>naval_hit_chance</c>). Defaults are vanilla 1.19.3; the loader
/// replaces whole blocks with the ones found in the game and mod folders it is given.
/// </summary>
public sealed class StaticModifiers
{
    public const string ScreeningBonus = "screening_bonus";
    public const string CapitalScreeningBonus = "capital_screening_bonus";
    public const string ShipExperienceBonusMax = "ship_experience_bonus_max";
    public const string ShipExperienceMalusMin = "ship_experience_malus_min";
    public const string CarrierExperienceBonusMax = "carrier_experience_bonus_max";
    public const string CarrierExperienceMalusMin = "carrier_experience_malus_min";
    public const string Night = "night";

    public const string NavalHitChance = "naval_hit_chance";
    public const string NavalRetreatSpeed = "naval_retreat_speed";
    public const string NavalRetreatSpeedAfterInitialCombat = "naval_retreat_speed_after_initial_combat";
    public const string SubRetreatSpeed = "sub_retreat_speed";
    public const string ConvoyRetreatSpeed = "convoy_retreat_speed";
    public const string SortieEfficiency = "sortie_efficiency";
    public const string NavalDamageFactor = "naval_damage_factor";
    public const string NavalDefenseFactor = "naval_defense_factor";
    public const string FighterSortieEfficiency = "fighter_sortie_efficiency";
    public const string CarrierTraffic = "carrier_traffic";
    public const string NavalLightGunHitChanceFactor = "naval_light_gun_hit_chance_factor";
    public const string NavalHeavyGunHitChanceFactor = "naval_heavy_gun_hit_chance_factor";
    public const string NavalTorpedoHitChanceFactor = "naval_torpedo_hit_chance_factor";

    public static IReadOnlyCollection<string> UsedBlocks { get; } =
    [
        ScreeningBonus, CapitalScreeningBonus, ShipExperienceBonusMax, ShipExperienceMalusMin,
        CarrierExperienceBonusMax, CarrierExperienceMalusMin, Night
    ];

    public Dictionary<string, Dictionary<string, double>> Blocks { get; } = new(StringComparer.Ordinal)
    {
        [ScreeningBonus] = new(StringComparer.Ordinal) { [NavalRetreatSpeed] = 0.2, [ConvoyRetreatSpeed] = 0.2, [NavalHitChance] = 0.4 },
        [CapitalScreeningBonus] = new(StringComparer.Ordinal) { [NavalRetreatSpeed] = 0.2, [SortieEfficiency] = 0.1 },
        [ShipExperienceBonusMax] = new(StringComparer.Ordinal) { [NavalDamageFactor] = 0.3, [NavalDefenseFactor] = 0.2 },
        [ShipExperienceMalusMin] = new(StringComparer.Ordinal) { [NavalDamageFactor] = -0.1, [NavalDefenseFactor] = -0.1 },
        [CarrierExperienceBonusMax] = new(StringComparer.Ordinal) { [FighterSortieEfficiency] = 0.2 },
        [CarrierExperienceMalusMin] = new(StringComparer.Ordinal) { [FighterSortieEfficiency] = -0.1 },
        [Night] = new(StringComparer.Ordinal) { [NavalHitChance] = -0.25, [NavalRetreatSpeed] = 0.1, [CarrierTraffic] = -1.0 }
    };

    public double Get(string block, string modifier)
    {
        return Blocks.TryGetValue(block, out var values) && values.TryGetValue(modifier, out var value) ? value : 0;
    }
}
