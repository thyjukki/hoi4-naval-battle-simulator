namespace NavySimulator.Domain;

public static class Hoi4Defines
{
    public const double SCREEN_RATIO_FOR_FULL_SCREENING_FOR_CAPITALS = 3.0;
    public const double SCREEN_RATIO_FOR_FULL_SCREENING_FOR_CONVOYS = 0.5;
    public const double CAPITAL_RATIO_FOR_FULL_SCREENING_FOR_CARRIERS = 1.0;
    public const double CAPITAL_RATIO_FOR_FULL_SCREENING_FOR_CONVOYS = 0.25;

    public const int BASE_GUN_COOLDOWNS_LIGHT = 2;
    public const int BASE_GUN_COOLDOWNS_HEAVY = 3;
    public const int BASE_GUN_COOLDOWNS_TORPEDO = 5;
    public const int BASE_GUN_COOLDOWNS = 1;

    public const double DEPTH_CHARGES_HIT_CHANCE_MULT = 1.25;
    public const double DEPTH_CHARGES_HIT_PROFILE = 80.0;
    
    public const double COMBAT_BASE_HIT_CHANCE = 0.1;
    public const double COMBAT_MIN_HIT_CHANCE = 0.02;

    public const double GUN_HIT_PROFILES_LIGHT = 60.0;
    public const double GUN_HIT_PROFILES_HEAVY = 95.0;
    public const double GUN_HIT_PROFILES_TORPEDO = 130.0;

    public const double PositioningBaseContribution = 0.5;
    public const double PositioningContributionScale = 0.5;

    public const double COMBAT_RETREAT_DECISION_CHANCE = 0.2;
    public const double CombatMinStrRetreatChance = 0.4; //Testing, normally 4 this is REPAIR_AND_RETURN_PRIO_HIGH_COMBAT
    public const double BASE_ESCAPE_SPEED = 0.06;
    public const double SPEED_TO_ESCAPE_SPEED = 1.15;
    public const double MAX_ESCAPE_SPEED_FROM_COMBAT_DURATION = 0.20;
    public const double ESCAPE_SPEED_PER_COMBAT_DAY = 0.01;

    public const double ScreeningBonusRetreatSpeed = 0.25;
    public const double CapitalScreeningBonusRetreatSpeed = 0.2;

    public const double ScreeningVisibiliityBonus = -0.1;
    
    public const double NightRetreatSpeed = 0.1;
    public const double NightHitChance = -0.25;
    public const double NightCarrierTraffic = -0.75;
    public const double NightTorpedoHitChanceFactor = -0.15;

    public const double BASE_POSITIONING = 1.0;
    public const double RELATIVE_SURFACE_DETECTION_TO_POSITIONING_FACTOR = 0.01;
    public const double MAX_POSITIONING_BONUS_FROM_SURFACE_DETECTION = 0.05;

    public const double HIGHER_SHIP_RATIO_POSITIONING_PENALTY_FACTOR = 0.4; // if one side has more ships than the other, that side will get this penalty for each +100% ship ratio it has
    public const double MAX_POSITIONING_PENALTY_FROM_HIGHER_SHIP_RATIO = 1.2;  // maximum penalty to get from larger fleets
    public const double MIN_SHIPS_FOR_HIGHER_SHIP_RATIO_PENALTY = 99;    // the minimum fleet size in ships that a fleet must be before having the large fleet penalty applied to them
    
    public const double DAMAGE_PENALTY_ON_MINIMUM_POSITIONING = 0.75;	// damage penalty at 0% positioning
    public const double SCREENING_EFFICIENCY_PENALTY_ON_MINIMUM_POSITIONING = 0.4;  // screening efficiency (screen to capital ratio) at 0% positioning
    public const double AA_EFFICIENCY_PENALTY_ON_MINIMUM_POSITIONING = 0.4;  // AA penalty at 0% positioning
    public const double SUBMARINE_REVEAL_ON_MINIMUM_POSITIONING = 2.0;  // submarine reveal change on 0% positioning

    public const double HIT_PROFILE_MULT = 100.0;
    public const double HIT_PROFILE_SPEED_FACTOR = 0.85;
    public const double HIT_PROFILE_SPEED_BASE = 5.0;

    public const double COMBAT_LOW_ORG_HIT_CHANCE_PENALTY = -0.5;

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

    public const double NAVAL_STRIKE_DAMAGE_TO_STR = 1.25;
    public const double NAVAL_STRIKE_DAMAGE_TO_ORG = 1.25;

    public static readonly double[] NAVY_PIERCING_THRESHOLDS =
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

    public static readonly double[] NAVY_PIERCING_THRESHOLD_DAMAGE_VALUES =
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

    public static readonly int CARRIER_HOURS_DELAY_AFTER_EACH_COMBAT = 8;
    
    public static readonly double NAVAL_COMBAT_AIR_SUB_TARGET_SCALE = 10;                             // scaled scoring for target picking for planes inside naval combat, max value when zero screening efficency, one define per ship typ
    public static readonly double NAVAL_COMBAT_AIR_SCREEN_TARGET_SCALE = 10;
    public static readonly double NAVAL_COMBAT_AIR_CAPITAL_TARGET_SCALE = 30;
    public static readonly double NAVAL_COMBAT_AIR_CARRIER_TARGET_SCALE = 100;
    public static readonly double NAVAL_COMBAT_AIR_STRENGTH_TARGET_SCORE = 5;                         // how much score factor from low health (scales between 0->this number)
    public static readonly double NAVAL_COMBAT_AIR_LOW_AA_TARGET_SCORE = 5;

    public static readonly double NAVAL_COMBAT_EXTERNAL_PLANES_JOIN_RATIO = 0.1;
    public static readonly double NAVAL_COMBAT_EXTERNAL_PLANES_JOIN_RATIO_PER_DAY = 0.25;
    public static readonly double NAVAL_COMBAT_EXTERNAL_PLANES_MIN_CAP = 30;

    public static readonly double ANTI_AIR_TARGETTING_TO_CHANCE = 0.2;
    public static readonly double ANTI_AIR_TARGETING = 0.4;
    public static readonly double AIR_AGILITY_TO_NAVAL_STRIKE_AGILITY = 0.02;
    public static readonly double ANTI_AIR_ATTACK_TO_AMOUNT = 0.01;
    public static double MAX_ANTI_AIR_REDUCTION_EFFECT_ON_INCOMING_AIR_DAMAGE = 0.75;
    public static readonly double ANTI_AIR_MULT_ON_INCOMING_AIR_DAMAGE = 0.03;
    public static readonly double ANTI_AIR_POW_ON_INCOMING_AIR_DAMAGE = 0.58;
    public static readonly double SHIP_TO_FLEET_ANTI_AIR_RATIO = 0.25;

    public static readonly double NAVAL_STRIKE_CARRIER_MULTIPLIER = 1.75;
    public static readonly double NAVAL_STRIKE_TARGETTING_TO_AMOUNT = 0.4;

    public const double BASE_CARRIER_SORTIE_EFFICIENCY = 0.20;
    public const double CARRIER_SORTIE_EFFICIENCY_FROM_SCREENING = 0.35;
    public const double CARRIER_SORTIE_EFFICIENCY_FROM_CAPITAL_SCREENING = 0.20;

    public const int COMBAT_MIN_DURATION = 8;
    public const int COMBAT_INITIAL_DURATION = 6;
    public static readonly int ALL_SHIPS_ACTIVATE_TIME = 12;
    public static readonly int CAPITAL_ONLY_COMBAT_ACTIVATE_TIME = 8;
    public static readonly int CARRIER_ONLY_COMBAT_ACTIVATE_TIME = 0;

    public const int SHIP_EXPERIENCE_LEVEL_UNTRAINED = 0;
    public const int SHIP_EXPERIENCE_LEVEL_REGULAR = 1;
    public const int SHIP_EXPERIENCE_LEVEL_TRAINED = 2;

    public const double SHIP_EXPERIENCE_ATTACK_MODIFIER_UNTRAINED = -0.10;
    public const double SHIP_EXPERIENCE_ATTACK_MODIFIER_REGULAR = 0.0;
    public const double SHIP_EXPERIENCE_ATTACK_MODIFIER_TRAINED = 0.06;

    public static double GetShipExperienceAttackModifier(int experienceLevel)
    {
        return experienceLevel switch
        {
            SHIP_EXPERIENCE_LEVEL_UNTRAINED => SHIP_EXPERIENCE_ATTACK_MODIFIER_UNTRAINED,
            SHIP_EXPERIENCE_LEVEL_REGULAR => SHIP_EXPERIENCE_ATTACK_MODIFIER_REGULAR,
            SHIP_EXPERIENCE_LEVEL_TRAINED => SHIP_EXPERIENCE_ATTACK_MODIFIER_TRAINED,
            _ => SHIP_EXPERIENCE_ATTACK_MODIFIER_REGULAR
        };
    }

    public const int NightEndHour = 0; // 5
    public const int NightStartHour = 24; //17
}

