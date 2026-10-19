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

    /// <summary>S-POTF: the block the pride of the fleet ship itself carries in every modifier read.</summary>
    public const string PrideOfTheFleet = "pride_of_the_fleet";

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
    public const string CarrierNightTraffic = "carrier_night_traffic";
    public const string Positioning = "positioning";
    public const string ScreeningEfficiency = "screening_efficiency";
    public const string ScreeningWithoutScreens = "screening_without_screens";
    public const string NavalTorpedoScreenPenetrationFactor = "naval_torpedo_screen_penetration_factor";
    public const string NavalTorpedoRevealChanceFactor = "naval_torpedo_reveal_chance_factor";
    public const string NavalTorpedoCooldownFactor = "naval_torpedo_cooldown_factor";
    public const string SubmarineAttack = "submarine_attack";
    public const string NavySubmarineAttackFactor = "navy_submarine_attack_factor";
    public const string NavySubmarineDefenceFactor = "navy_submarine_defence_factor";
    public const string NavyCapitalShipAttackFactor = "navy_capital_ship_attack_factor";
    public const string NavyCapitalShipDefenceFactor = "navy_capital_ship_defence_factor";
    public const string NavyScreenAttackFactor = "navy_screen_attack_factor";
    public const string NavyScreenDefenceFactor = "navy_screen_defence_factor";
    public const string NavalNightAttack = "naval_night_attack";
    public const string NavyAntiAirAttack = "navy_anti_air_attack";
    public const string NavyAntiAirAttackFactor = "navy_anti_air_attack_factor";
    public const string NavyVisibility = "navy_visibility";
    public const string NavyCasualtyOnHit = "navy_casualty_on_hit";
    public const string NavalStrikeAttackFactor = "naval_strike_attack_factor";
    public const string NavalStrikeTargettingFactor = "naval_strike_targetting_factor";
    public const string NavalStrikeAgilityFactor = "naval_strike_agility_factor";
    public const string NavyCarrierAirAttackFactor = "navy_carrier_air_attack_factor";
    public const string NavyCarrierAirTargettingFactor = "navy_carrier_air_targetting_factor";
    public const string NavyCarrierAirAgilityFactor = "navy_carrier_air_agility_factor";
    public const string NavalLightGunHitChanceFactor = "naval_light_gun_hit_chance_factor";
    public const string NavalHeavyGunHitChanceFactor = "naval_heavy_gun_hit_chance_factor";
    public const string NavalTorpedoHitChanceFactor = "naval_torpedo_hit_chance_factor";
    public const string NavalCriticalScoreChanceFactor = "naval_critical_score_chance_factor";
    public const string CriticalReceiveChance = "critical_receive_chance";
    public const string NavalCriticalEffectFactor = "naval_critical_effect_factor";
    public const string NavalSpeedFactor = "naval_speed_factor";
    public const string NavyOrg = "navy_org";
    public const string NavyOrgFactor = "navy_org_factor";
    public const string NavalMorale = "naval_morale";
    public const string NavalMoraleFactor = "naval_morale_factor";
    public const string NavalRetreatChance = "naval_retreat_chance";
    public const string NavalRetreatChanceAfterInitialCombat = "naval_retreat_chance_after_initial_combat";
    public const string NavalEnemyRetreatChance = "naval_enemy_retreat_chance";
    public const string NavalEnemyFleetSizeRatioPenaltyFactor = "naval_enemy_fleet_size_ratio_penalty_factor";
    public const string NavalHasPotfInCombatAttack = "naval_has_potf_in_combat_attack";
    public const string NavalHasPotfInCombatDefense = "naval_has_potf_in_combat_defense";
    public const string ExperienceGainNavyUnitFactor = "experience_gain_navy_unit_factor";
    public const string ExperienceGainNavyUnit = "experience_gain_navy_unit";

    public static IReadOnlyCollection<string> UsedBlocks { get; } =
    [
        ScreeningBonus, CapitalScreeningBonus, ShipExperienceBonusMax, ShipExperienceMalusMin,
        CarrierExperienceBonusMax, CarrierExperienceMalusMin, Night, PrideOfTheFleet
    ];

    public Dictionary<string, Dictionary<string, double>> Blocks { get; } = new(StringComparer.Ordinal)
    {
        [ScreeningBonus] = new(StringComparer.Ordinal) { [NavalRetreatSpeed] = 0.2, [ConvoyRetreatSpeed] = 0.2, [NavalHitChance] = 0.4 },
        [CapitalScreeningBonus] = new(StringComparer.Ordinal) { [NavalRetreatSpeed] = 0.2, [SortieEfficiency] = 0.1 },
        [ShipExperienceBonusMax] = new(StringComparer.Ordinal) { [NavalDamageFactor] = 0.3, [NavalDefenseFactor] = 0.2 },
        [ShipExperienceMalusMin] = new(StringComparer.Ordinal) { [NavalDamageFactor] = -0.1, [NavalDefenseFactor] = -0.1 },
        [CarrierExperienceBonusMax] = new(StringComparer.Ordinal) { [FighterSortieEfficiency] = 0.2 },
        [CarrierExperienceMalusMin] = new(StringComparer.Ordinal) { [FighterSortieEfficiency] = -0.1 },
        [Night] = new(StringComparer.Ordinal) { [NavalHitChance] = -0.25, [NavalRetreatSpeed] = 0.1, [CarrierTraffic] = -1.0 },
        [PrideOfTheFleet] = new(StringComparer.Ordinal) { [ExperienceGainNavyUnitFactor] = 0.25, [CriticalReceiveChance] = -0.5 }
    };

    public double Get(string block, string modifier)
    {
        return Blocks.TryGetValue(block, out var values) && values.TryGetValue(modifier, out var value) ? value : 0;
    }
}
