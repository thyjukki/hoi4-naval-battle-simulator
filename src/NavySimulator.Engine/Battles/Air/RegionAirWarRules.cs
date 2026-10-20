using NavySimulator.Engine.Defines;
using NavySimulator.Engine.Random;
using NavySimulator.Engine.Stats;

namespace NavySimulator.Engine.Battles.Air;

/// <summary>
/// A-REGION [RE + LOG]: formulas of the region's air war that the battle's air wings fight in each air hour, before the
/// naval combat hour (0xc5f4c0, 0xf7be70, 0xf862a0, 0xf77360, 0xc4c570, 0xf78160). Spec: docs/mechanics/air.md;
/// finding A-REGION-carrier-wings-in-the-region-air-war.md.
/// </summary>
public static class RegionAirWarRules
{
    /// <summary>
    /// [RE] 0xf5e890 / 0xf60860 (A-STATS-GETTERS): a wing's air combat stats, absolute and divided by AIR_WING_MAX_STATS_*:
    /// attack, defence and agility × (M + 1 + the country's and the wing experience holder's air_attack/defence/agility_factor),
    /// truncated; speed with the ace's speed bonus. Only <paramref name="inRegion"/> reads (the province is given: the
    /// attacker and defender in a fight, the strike efficiency) of a wing with a region mission take the mission
    /// efficiency M and the home defence or power projection factor (attack and defence); the sort and escorts do not.
    /// Region weather and night terms are not modelled (0 for TFB; vanilla night has air_attack_factor −0.5).
    /// </summary>
    public readonly record struct WingStats(double Attack, double Defence, double Agility, double Speed, double AttackShare,
        double DefenceShare, double AgilityShare, double SpeedShare);

    public static WingStats Stats(AirWing wing, GameDefines defines, ModifierSet? country = null, bool inRegion = false)
    {
        var air = defines.NAir;
        var modifiers = country ?? ModifierSet.Empty;
        var stats = wing.Stats;
        double Factor(string key) => 1 + modifiers.Get(key) + ExperienceModifier(wing, key, modifiers, defines);
        var attackFactor = Factor(StaticModifiers.AirAttackFactor);
        var defenceFactor = Factor(StaticModifiers.AirDefenceFactor);
        var agilityFactor = Factor(StaticModifiers.AirAgilityFactor);
        var mission = 0.0;

        if (inRegion && wing.RegionMission is { } region)
        {
            mission = region.EfficiencyKey is null ? 0 : modifiers.Get(region.EfficiencyKey);
            var reach = modifiers.Get(region.Home ? StaticModifiers.AirHomeDefenceFactor : StaticModifiers.AirPowerProjectionFactor);
            attackFactor += reach;
            defenceFactor += reach;
        }

        var attack = FixedPoint.Truncate((mission + attackFactor) * stats.AirAttack);
        var defence = FixedPoint.Truncate((mission + defenceFactor) * stats.AirDefense);
        var agility = FixedPoint.Truncate((mission + agilityFactor) * stats.Agility);
        var speed = stats.Speed * (1 + wing.AceBonus(StaticModifiers.AirMaximumSpeedFactor, defines));
        return new WingStats(attack, defence, agility, speed, attack / air.AIR_WING_MAX_STATS_ATTACK, defence / air.AIR_WING_MAX_STATS_DEFENCE,
            agility / air.AIR_WING_MAX_STATS_AGILITY, speed / air.AIR_WING_MAX_STATS_SPEED);
    }

    /// <summary>
    /// [RE] 0xf862a0: the planes one attacker shoots down of one defender, before the cap: the damage share × the
    /// attacker's attack × COMBAT_DAMAGE_STATS_MULTILPIER, less the defender's agility edge, plus the attacker's speed edge
    /// and its top-speed bonus, then x = trunc(trunc(scale × damage) × 0.01) / the defender's defence, at least 0.001.
    /// <paramref name="bothCarriers"/> uses COMBAT_DAMAGE_SCALE_CARRIER, else COMBAT_DAMAGE_SCALE.
    /// </summary>
    public static double ExpectedLosses(double potential, WingStats attacker, WingStats defender, bool bothCarriers, GameDefines defines)
    {
        var air = defines.NAir;
        var damage = potential * air.COMBAT_DAMAGE_STATS_MULTILPIER;
        var reduction = attacker.AgilityShare < defender.AgilityShare && attacker.AgilityShare > 0
            ? (Math.Min(defender.AgilityShare / attacker.AgilityShare, air.BIGGEST_AGILITY_FACTOR_DIFF) - 1) *
              FixedPoint.Truncate(damage * air.COMBAT_BETTER_AGILITY_DAMAGE_REDUCTION)
            : 0;
        var increase = attacker.Speed > defender.Speed && defender.Speed > 0
            ? (Math.Min(attacker.SpeedShare / defender.SpeedShare, air.BIGGEST_SPEED_FACTOR_DIFF) - 1) *
              FixedPoint.Truncate(damage * air.COMBAT_BETTER_SPEED_DAMAGE_INCREASE) +
              FixedPoint.Truncate(Math.Truncate(attacker.Speed / 100) * air.TOP_SPEED_DAMAGE_BONUS_FACTOR) * damage
            : 0;
        damage += increase - reduction;
        var scale = bothCarriers ? air.COMBAT_DAMAGE_SCALE_CARRIER : air.COMBAT_DAMAGE_SCALE;
        return defender.Defence > 0
            ? Math.Max(0.001, FixedPoint.Truncate(FixedPoint.Truncate(scale * damage) * defines.Hardcoded.AirCombatLossScale) / defender.Defence)
            : double.PositiveInfinity;
    }

    /// <summary>[RE] 0xf862a0: the planes lost, at most <paramref name="cap"/>; below it the fraction is rolled.</summary>
    public static int Losses(double expected, int cap, IRandomSource random)
    {
        if (cap <= 0)
        {
            return 0;
        }

        if (expected >= cap)
        {
            return cap;
        }

        var whole = (int)Math.Floor(expected);
        return whole + (random.NextDouble(RollKind.RegionAirLoss) < expected - whole ? 1 : 0);
    }

    /// <summary>
    /// [RE] 0xf7be70: the disruption an attacker of <paramref name="planes"/> effective planes spreads over the strike
    /// missions it meets: trunc(DISRUPTION_FACTOR(_CARRIER) × planes × (1 + speed × DISRUPTION_SPEED_FACTOR) × (1 + agility
    /// × DISRUPTION_AGILITY_FACTOR) × (1 + attack × DISRUPTION_ATTACK_FACTOR) × (1 + detection × DISRUPTION_DETECTION_FACTOR)),
    /// with the attacking side's <paramref name="detection"/> (A-DETECT).
    /// </summary>
    public static double Disruption(int planes, WingStats attacker, bool bothCarriers, GameDefines defines, double detection)
    {
        var air = defines.NAir;
        var potential = planes * (1 + attacker.SpeedShare * air.DISRUPTION_SPEED_FACTOR) * (1 + attacker.AgilityShare * air.DISRUPTION_AGILITY_FACTOR) *
                        (1 + attacker.AttackShare * air.DISRUPTION_ATTACK_FACTOR) *
                        (1 + detection * air.DISRUPTION_DETECTION_FACTOR);
        return FixedPoint.Truncate(Math.Max(0, (bothCarriers ? air.DISRUPTION_FACTOR_CARRIER : air.DISRUPTION_FACTOR) * potential));
    }

    /// <summary>
    /// [RE] 0xf77360: an air superiority mission's escort value, which cancels its side's disruption (0xc4c570); the
    /// <paramref name="detection"/> is its own side's (A-DETECT).
    /// </summary>
    public static double Escort(int planes, WingStats wing, GameDefines defines, ModifierSet? country = null, double detection = 0)
    {
        var air = defines.NAir;
        return planes * (1 + wing.SpeedShare * air.ESCORT_SPEED_FACTOR) * (1 + wing.AgilityShare * air.ESCORT_AGILITY_FACTOR) *
               (1 + wing.AttackShare * air.ESCORT_ATTACK_FACTOR) * (1 + detection * air.DISRUPTION_DETECTION_FACTOR) *
               air.ESCORT_FACTOR * (1 + (country ?? ModifierSet.Empty).Get(StaticModifiers.AirEscortEfficiency));
    }

    /// <summary>
    /// [RE] A-REGION-SYNC 0xf78500: a wing's mission effectiveness, clamp(base × crowding × (1 + air_mission_efficiency), 0,
    /// 1). Carrier wings: base = supply ratio × fuel (an input), crowding = the deck's (A-SORTIE), no coverage and no region
    /// change penalty; land wings: base = the input that stands for coverage, base crowding, supply, region change penalty
    /// and fuel. air_mission_efficiency = the country's + the carrier fleet's admiral's + the region weather's (wings with a
    /// region mission only).
    /// </summary>
    public static double Effectiveness(double baseEfficiency, double crowding, double missionEfficiencyModifier)
    {
        return Math.Clamp(baseEfficiency * crowding * (1 + missionEfficiencyModifier), 0, 1);
    }

    /// <summary>A-DETECT: one of the attacking country's missions on the region's mission list, as the detection counts it.</summary>
    public readonly record struct DetectionMission(int Planes, bool AirSuperiority, bool Interception, bool Home, double NightFactor, double WeatherFactor);

    /// <summary>
    /// [RE] A-DETECT 0xf77730: a country's air detection in the region, clamp(trunc((AIR + RAD + BASE) × (1 + NIGHT + WEA)),
    /// 0.01, 1). AIR = trunc(clamp(Σ trunc((1 + t + home ? air_home_defence_factor : air_power_projection_factor) × planes)
    /// / DETECT_CHANCE_FROM_AIRCRAFTS_EFFECTIVE_COUNT, 0, 1) × DETECT_CHANCE_FROM_AIRCRAFTS) over its region-list missions,
    /// t = air_superiority_detect_factor, air_interception_detect_factor or NO_SEARCH_MISSION_DETECT_FACTOR by mission;
    /// RAD = trunc(radar coverage × DETECT_CHANCE_FROM_RADARS); BASE = trunc(controlled land share ×
    /// DETECT_CHANCE_FROM_OCCUPATION) (0 in a sea region); NIGHT = trunc(trunc(region darkness × DETECT_CHANCE_FROM_NIGHT) ×
    /// the plane-weighted night factor); WEA = trunc(the region weather's air_detection × the plane-weighted weather factor).
    /// </summary>
    public static double Detection(IReadOnlyList<DetectionMission> missions, ModifierSet country, double radarCoverage, double controlledLandShare,
        double regionDarkness, double weatherAirDetection, GameDefines defines)
    {
        var air = defines.NAir;
        var count = 0.0;
        var planes = 0;
        var night = 0.0;
        var weather = 0.0;

        foreach (var mission in missions)
        {
            planes += mission.Planes;
            night += FixedPoint.Truncate(mission.NightFactor * mission.Planes);
            weather += FixedPoint.Truncate(mission.WeatherFactor * mission.Planes);
            var search = mission.AirSuperiority ? country.Get(StaticModifiers.AirSuperiorityDetectFactor)
                : mission.Interception ? country.Get(StaticModifiers.AirInterceptionDetectFactor)
                : air.NO_SEARCH_MISSION_DETECT_FACTOR;
            var reach = mission.Home ? country.Get(StaticModifiers.AirHomeDefenceFactor) : country.Get(StaticModifiers.AirPowerProjectionFactor);
            count += FixedPoint.Truncate((1 + search + reach) * mission.Planes);
        }

        var ratio = air.DETECT_CHANCE_FROM_AIRCRAFTS_EFFECTIVE_COUNT > 0 ? Math.Clamp(count / air.DETECT_CHANCE_FROM_AIRCRAFTS_EFFECTIVE_COUNT, 0, 1) : 1;
        var aircraft = FixedPoint.Truncate(ratio * air.DETECT_CHANCE_FROM_AIRCRAFTS);
        var radar = FixedPoint.Truncate(Math.Clamp(radarCoverage, 0, 1) * air.DETECT_CHANCE_FROM_RADARS);
        var territory = FixedPoint.Truncate(Math.Clamp(controlledLandShare, 0, 1) * air.DETECT_CHANCE_FROM_OCCUPATION);
        var nightTerm = planes > 0 ? FixedPoint.Truncate(FixedPoint.Truncate(regionDarkness * air.DETECT_CHANCE_FROM_NIGHT) * (night / planes)) : 0;
        var weatherTerm = planes > 0 ? FixedPoint.Truncate(weatherAirDetection * (weather / planes)) : 0;
        var total = FixedPoint.Truncate((aircraft + radar + territory) * (1 + nightTerm + weatherTerm));
        return Math.Clamp(total, defines.Hardcoded.MinimumRegionAirDetection, 1);
    }

    /// <summary>
    /// [RE] A-STATS-XP 0xf65f20: the wing experience holder's value of <paramref name="key"/>: with v = clamp(xp / L0 − 1, −1, 0)
    /// below the first AIR_WING_XP_LEVELS level, else clamp((xp − L0) / (Llast − L0), 0, 1), truncated to 0.01:
    /// air_wing_experience_bonus_max × v, or air_wing_experience_malus_min × (−v) × (1 + max(air_untrained_pilots_penalty_factor, −1)).
    /// </summary>
    public static double ExperienceModifier(AirWing wing, string key, ModifierSet country, GameDefines defines)
    {
        var levels = defines.NAir.AIR_WING_XP_LEVELS;

        if (levels.Length == 0 || levels[0] <= 0)
        {
            return 0;
        }

        var first = levels[0];
        var last = levels[^1];
        var v = wing.Experience < first
            ? Math.Clamp(wing.Experience / first - 1, -1, 0)
            : last > first ? Math.Clamp((wing.Experience - first) / (last - first), 0, 1) : 1;
        v = Math.Truncate(v * 100) / 100;

        if (v > 0)
        {
            return defines.StaticModifiers.Get(StaticModifiers.AirWingExperienceBonusMax, key) * v;
        }

        var untrained = 1 + Math.Max(country.Get(StaticModifiers.AirUntrainedPilotsPenaltyFactor), -1);
        return defines.StaticModifiers.Get(StaticModifiers.AirWingExperienceMalusMin, key) * -v * untrained;
    }

    /// <summary>
    /// [RE] A-ACE 0xf5c9d0: a wing's weather factor, clamp(1 + the owner's and the experience holder's air_weather_penalty,
    /// 0, 2) (ace terms not modelled).
    /// </summary>
    public static double WeatherFactor(AirWing wing, ModifierSet country, GameDefines defines)
    {
        return Math.Clamp(1 + country.Get(StaticModifiers.AirWeatherPenalty) +
                          ExperienceModifier(wing, StaticModifiers.AirWeatherPenalty, country, defines), 0, 2);
    }

    /// <summary>
    /// [RE] A-ACE 0xf5bc80: a wing's night factor, clamp(1 + the owner's and the experience holder's air_night_penalty, 0,
    /// 2) (the plane's night stat, ace and carrier-mission terms not modelled).
    /// </summary>
    public static double NightFactor(AirWing wing, ModifierSet country, GameDefines defines)
    {
        return Math.Clamp(1 + country.Get(StaticModifiers.AirNightPenalty) +
                          ExperienceModifier(wing, StaticModifiers.AirNightPenalty, country, defines), 0, 2);
    }

    /// <summary>
    /// [RE + LOG] 0xf78160: the mission efficiency a disrupted wing strikes with, clamp(1 − trunc(sqrt(disruption / den) ×
    /// 0.316228), 0.001, 1), den = planes × (1 + defence × DISRUPTION_DEFENCE_DEFENCE_FACTOR) × (1 + attack ×
    /// DISRUPTION_DEFENCE_ATTACK_FACTOR) × (1 + speed × DISRUPTION_DEFENCE_SPEED_FACTOR) with the wing's shares. [LOG] It
    /// matched the game's strike averages of disrupted carrier bombers (0.67 at disruption 14.4) and land bombers (0.956).
    /// </summary>
    public static double StrikeEfficiency(double disruption, double planes, AirWing wing, GameDefines defines, ModifierSet? country = null)
    {
        if (disruption <= 0 || planes <= 0)
        {
            return 1;
        }

        var air = defines.NAir;
        var stats = Stats(wing, defines, country, inRegion: true);

        if (stats.DefenceShare <= 0)
        {
            return 1;
        }

        var den = planes * (1 + stats.DefenceShare * air.DISRUPTION_DEFENCE_DEFENCE_FACTOR) * (1 + stats.AttackShare * air.DISRUPTION_DEFENCE_ATTACK_FACTOR) *
                  (1 + stats.SpeedShare * air.DISRUPTION_DEFENCE_SPEED_FACTOR);
        return Math.Clamp(1 - FixedPoint.Truncate(Math.Sqrt(disruption / den) * defines.Hardcoded.DisruptionEfficiencyScale), 0.001, 1);
    }
}
