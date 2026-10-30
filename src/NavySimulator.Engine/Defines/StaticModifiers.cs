namespace NavySimulator.Engine.Defines;

/// <summary>
/// Values from <c>common/modifiers/00_static_modifiers.txt</c> that naval combat applies directly, keyed by block and
/// modifier name (e.g. <c>screening_bonus</c> / <c>naval_hit_chance</c>). Defaults are vanilla 1.19.4 (the same as
/// 1.19.3); the loader replaces whole blocks with the ones found in the game and mod folders it is given.
/// </summary>
public sealed class StaticModifiers
{
    public const string ScreeningBonus = "screening_bonus";
    public const string CapitalScreeningBonus = "capital_screening_bonus";
    public const string ShipExperienceBonusMax = "ship_experience_bonus_max";
    public const string ShipExperienceMalusMin = "ship_experience_malus_min";
    public const string CarrierExperienceBonusMax = "carrier_experience_bonus_max";
    public const string CarrierExperienceMalusMin = "carrier_experience_malus_min";
    public const string AirWingExperienceBonusMax = "air_wing_experience_bonus_max";
    public const string AirWingExperienceMalusMin = "air_wing_experience_malus_min";
    public const string Night = "night";

    /// <summary>S-WEATHER: the region weather blocks (<c># on Region</c>), by the phenomenon of the battle's region.</summary>
    public const string WeatherRainLight = "weather_rain_light";
    public const string WeatherRainHeavy = "weather_rain_heavy";
    public const string WeatherSnow = "weather_snow";
    public const string WeatherBlizzard = "weather_blizzard";
    public const string WeatherSandstorm = "weather_sandstorm";
    public const string WeatherArcticWater = "weather_arctic_water";

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
    public const string NavalStrike = "naval_strike";
    public const string NavyWeatherPenalty = "navy_weather_penalty";
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
    public const string NavalDetection = "naval_detection";
    public const string NavyCasualtyOnHit = "navy_casualty_on_hit";
    public const string NavalStrikeAttackFactor = "naval_strike_attack_factor";
    public const string NavalStrikeTargettingFactor = "naval_strike_targetting_factor";
    public const string NavalStrikeAgilityFactor = "naval_strike_agility_factor";
    public const string AceEffectivenessFactor = "ace_effectiveness_factor";
    public const string AirAttackFactor = "air_attack_factor";
    public const string AirDefenceFactor = "air_defence_factor";
    public const string AirAgilityFactor = "air_agility_factor";
    public const string AirMaximumSpeedFactor = "air_maximum_speed_factor";
    public const string AirEscortEfficiency = "air_escort_efficiency";
    public const string CarrierCapacityPenaltyReduction = "carrier_capacity_penalty_reduction";
    public const string AirSuperiorityEfficiency = "air_superiority_efficiency";
    public const string AirInterceptEfficiency = "air_intercept_efficiency";
    public const string AirNavEfficiency = "air_nav_efficiency";
    public const string AirMissionEfficiency = "air_mission_efficiency";
    public const string AirMissionXpGainFactor = "air_mission_xp_gain_factor";
    public const string AirWingXpLossWhenKilledFactor = "air_wing_xp_loss_when_killed_factor";
    public const string AirRangeFactor = "air_range_factor";
    public const string AirAccidents = "air_accidents";
    public const string AirAccidentsFactor = "air_accidents_factor";
    public const string AirUntrainedPilotsPenaltyFactor = "air_untrained_pilots_penalty_factor";
    public const string AirSuperiorityDetectFactor = "air_superiority_detect_factor";
    public const string AirInterceptionDetectFactor = "air_interception_detect_factor";
    public const string AirHomeDefenceFactor = "air_home_defence_factor";
    public const string AirPowerProjectionFactor = "air_power_projection_factor";
    public const string AirNightPenalty = "air_night_penalty";
    public const string AirWeatherPenalty = "air_weather_penalty";
    public const string AirDetection = "air_detection";
    public const string AirAceGenerationChanceFactor = "air_ace_generation_chance_factor";
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
    public const string NavalShipRecoveryChance = "naval_ship_recovery_chance";
    public const string NavalShipRecoveryChanceFactor = "naval_ship_recovery_chance_factor";
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
        CarrierExperienceBonusMax, CarrierExperienceMalusMin, AirWingExperienceBonusMax, AirWingExperienceMalusMin, Night, PrideOfTheFleet,
        WeatherRainLight, WeatherRainHeavy, WeatherSnow, WeatherBlizzard, WeatherSandstorm, WeatherArcticWater
    ];

    public Dictionary<string, Dictionary<string, double>> Blocks { get; } = new(StringComparer.Ordinal)
    {
        [ScreeningBonus] = new(StringComparer.Ordinal) { [NavalRetreatSpeed] = 0.2, [ConvoyRetreatSpeed] = 0.2, [NavalHitChance] = 0.4 },
        [CapitalScreeningBonus] = new(StringComparer.Ordinal) { [NavalRetreatSpeed] = 0.2, [SortieEfficiency] = 0.1 },
        [ShipExperienceBonusMax] = new(StringComparer.Ordinal) { [NavalDamageFactor] = 0.3, [NavalDefenseFactor] = 0.2 },
        [ShipExperienceMalusMin] = new(StringComparer.Ordinal) { [NavalDamageFactor] = -0.1, [NavalDefenseFactor] = -0.1 },
        [CarrierExperienceBonusMax] = new(StringComparer.Ordinal) { [FighterSortieEfficiency] = 0.2 },
        [CarrierExperienceMalusMin] = new(StringComparer.Ordinal) { [FighterSortieEfficiency] = -0.1 },
        [AirWingExperienceBonusMax] = new(StringComparer.Ordinal)
        {
            [AirAttackFactor] = 0.2, [AirAgilityFactor] = 0.3, [AirNightPenalty] = -0.2, ["air_accidents_factor"] = -1.0
        },
        [AirWingExperienceMalusMin] = new(StringComparer.Ordinal)
        {
            [AirAttackFactor] = -0.15, [AirAgilityFactor] = -0.15, [AirNightPenalty] = 0.15, ["air_accidents_factor"] = 0.5
        },
        [Night] = new(StringComparer.Ordinal) { [NavalHitChance] = -0.25, [NavalRetreatSpeed] = 0.1, [CarrierTraffic] = -1.0 },
        [PrideOfTheFleet] = new(StringComparer.Ordinal) { [ExperienceGainNavyUnitFactor] = 0.25, [CriticalReceiveChance] = -0.5 },
        [WeatherRainLight] = Block(("air_accidents", 0.1), ("air_detection", -0.1), ("air_mission_efficiency", -0.1), ("local_org_regain", -0.05),
            ("naval_detection", -0.1), (NavalHitChance, -0.05), (NavalRetreatSpeed, 0.05), (NavalSpeedFactor, -0.05), (NavalStrike, -0.2),
            ("org_loss_when_moving", 0.05)),
        [WeatherRainHeavy] = Block(("air_accidents", 0.3), ("air_bombing_targetting", -0.5), ("air_detection", -0.2), ("air_mission_efficiency", -0.3),
            (CarrierTraffic, -0.8), ("local_org_regain", -0.15), ("naval_detection", -0.2), (NavalHitChance, -0.2), (NavalRetreatSpeed, 0.1),
            (NavalSpeedFactor, -0.1), (NavalStrike, -0.4), ("org_loss_when_moving", 0.1), (Positioning, -0.1), ("supply_consumption_factor", 0.1),
            ("supply_factor", -0.05)),
        [WeatherSnow] = Block(("air_accidents", 0.1), ("air_detection", -0.15), ("air_mission_efficiency", -0.1), ("naval_detection", -0.15),
            (NavalHitChance, -0.05), (NavalRetreatSpeed, 0.05), (NavalSpeedFactor, -0.05), (NavalStrike, -0.3), ("org_loss_when_moving", 0.1),
            (Positioning, -0.1), ("supply_factor", -0.10), ("truck_attrition_factor", 0.3)),
        [WeatherBlizzard] = Block(("air_accidents", 0.3), ("air_bombing_targetting", -0.7), ("air_detection", -0.3), ("air_mission_efficiency", -0.3),
            (CarrierTraffic, -1.0), ("local_org_regain", -0.1), ("naval_detection", -0.3), (NavalHitChance, -0.1), (NavalRetreatSpeed, 0.15),
            (NavalSpeedFactor, -0.1), (NavalStrike, -0.4), ("org_loss_when_moving", 0.25), (Positioning, -0.2), ("supply_consumption_factor", 0.15),
            ("supply_factor", -0.25), ("truck_attrition_factor", 1.0), ("army_attack_speed_factor", -0.25)),
        [WeatherSandstorm] = Block(("air_accidents", 0.6), ("air_bombing_targetting", -1), ("air_detection", -0.9), ("air_mission_efficiency", -0.5),
            ("local_org_regain", -0.2), (NavalStrike, -0.9), ("org_loss_when_moving", 0.25), ("supply_factor", -0.15), ("truck_attrition_factor", 3.0)),
        [WeatherArcticWater] = Block(("naval_attrition", 0.1), (NavalRetreatSpeed, -0.05), ("navy_casualty_on_sink", 0.2), (Positioning, -0.2))
    };

    private static Dictionary<string, double> Block(params (string Key, double Value)[] values)
    {
        return values.ToDictionary(value => value.Key, value => value.Value, StringComparer.Ordinal);
    }

    public double Get(string block, string modifier)
    {
        return Blocks.TryGetValue(block, out var values) && values.TryGetValue(modifier, out var value) ? value : 0;
    }
}
