namespace NavySimulator.Domain;

public static class Hoi4Defines
{
    public const double SCREEN_RATIO_FOR_FULL_SCREENING_FOR_CAPITALS = 3.0;
    public const double SCREEN_RATIO_FOR_FULL_SCREENING_FOR_CONVOYS = 0.5;
    public const double CAPITAL_RATIO_FOR_FULL_SCREENING_FOR_CARRIERS = 1.0;
    public const double CAPITAL_RATIO_FOR_FULL_SCREENING_FOR_CONVOYS = 0.25;

    public const int BASE_GUN_COOLDOWNS_LIGHT = 2;
    public const int BASE_GUN_COOLDOWNS_HEAVY = 5;
    public const int BASE_GUN_COOLDOWNS_TORPEDO = 2;
    public const int BASE_GUN_COOLDOWNS = 1;

    public const double DEPTH_CHARGES_HIT_CHANCE_MULT = 1.25;
    public const double DEPTH_CHARGES_HIT_PROFILE = 100.0;
    
    public const double COMBAT_BASE_HIT_CHANCE = 0.10;
    public const double COMBAT_MIN_HIT_CHANCE = 0.05;

    public const double GUN_HIT_PROFILES_LIGHT = 60.0;
    public const double GUN_HIT_PROFILES_HEAVY = 95.0;
    public const double GUN_HIT_PROFILES_TORPEDO = 60.0;

    public const double PositioningBaseContribution = 0.5;
    public const double PositioningContributionScale = 0.5;
    public const double DAMAGE_PENALTY_ON_MINIMUM_POSITIONING = 0.5;

    public const double COMBAT_MIN_DURATION = 8;
    public const double COMBAT_INITIAL_DURATION = 6;
    public const double COMBAT_RETREAT_DECISION_CHANCE = 0.2;
    public const double CombatMinStrRetreatChance = 0.4;
    public const double BASE_ESCAPE_SPEED = 0.06;
    public const double SPEED_TO_ESCAPE_SPEED = 1.15;
    public const double MAX_ESCAPE_SPEED_FROM_COMBAT_DURATION = 0.20;
    public const double ESCAPE_SPEED_PER_COMBAT_DAY = 0.01;

    public const double ScreeningBonusRetreatSpeed = 0.25;
    public const double CapitalScreeningBonusRetreatSpeed = 0.2;

    public const double ScreeningVisibiliityBonus = -0.1;
    
    public const double NightRetreatSpeed = 0.1;
    public const double NightHitChange = -0.25;
    public const double NightCarrierTraffic = -0.75;
    public const double NightTorpedoHitChangeFactor = -0.15;
    
    

    public const double HIT_PROFILE_MULT = 100.0;
    public const double HIT_PROFILE_SPEED_FACTOR = 0.85;
    public const double HIT_PROFILE_SPEED_BASE = 5.0;

    public const double TargetWeightCapitalLight = 2.0;
    public const double TargetWeightCapitalHeavyTorpedo = 30.0;
    public const double TargetWeightScreenLight = 6.0;
    public const double TargetWeightScreenHeavyTorpedo = 3.0;
    public const double TargetWeightCarrierLight = 1.0;
    public const double TargetWeightCarrierHeavyTorpedo = 15.0;
    public const double TargetWeightConvoyLight = 4.0;
    public const double TargetWeightConvoyHeavyTorpedo = 60.0;
    public const double TargetWeightSubmarine = 4.0;
    public const double TargetWeightDefault = 1.0;

    public const double RetreatingTargetWeightMult = 0.5;

    public const double COMBAT_DAMAGE_RANDOMNESS = 0.20;
    public const double COMBAT_DAMAGE_TO_STR_FACTOR = 0.5;
    public const double COMBAT_DAMAGE_TO_ORG_FACTOR = 1.0;

    public static double[] NAVY_PIERCING_THRESHOLDS =
    [
        2.00,
        1.50,
        1.00,
        0.90,
        0.75,
        0.50,
        0.25,
        0.10,
        0.00
    ];

    public static double[] NAVY_PIERCING_THRESHOLD_CRITICAL_VALUES =
    [
        1.75,
        1.10,
        1.00,
        0.90,
        0.75,
        0.50,
        0.25,
        0.10,
        0.00
    ];

    public static double[] NAVY_PIERCING_THRESHOLD_DAMAGE_VALUES =
    [
        1.00,
        1.00,
        1.00,
        0.85,
        0.70,
        0.40,
        0.30,
        0.20,
        0.10
    ];
}

