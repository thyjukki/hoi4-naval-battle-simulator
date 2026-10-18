-- Values from the previous simulator's Hoi4Defines.cs that differ from vanilla 1.17.
-- Commit history ("corrected defines to match tfb") says they were copied from the TFB mod's define file.
-- They have NOT been re-checked against that file: load the mod's own common/defines instead when it is available.
-- Use from a scenario with:  "defines": ["defines/tfb-legacy.lua"]

-- Surface combat
NDefines.NNavy.BASE_GUN_COOLDOWNS = { 3, 5, 2 }              -- { heavy, torpedo, light }; vanilla { 1, 4, 1 }
NDefines.NNavy.GUN_HIT_PROFILES = { 95.0, 130.0, 60.0 }      -- { heavy, torpedo, light }; vanilla { 80, 100, 45 }
NDefines.NNavy.COMBAT_MIN_HIT_CHANCE = 0.02
NDefines.NNavy.DEPTH_CHARGES_HIT_CHANCE_MULT = 1.25
NDefines.NNavy.DEPTH_CHARGES_HIT_PROFILE = 80.0
NDefines.NNavy.HIT_PROFILE_SPEED_FACTOR = 0.85
NDefines.NNavy.HIT_PROFILE_SPEED_BASE = 5.0
NDefines.NNavy.COMBAT_DAMAGE_RANDOMNESS = 0.20
NDefines.NNavy.COMBAT_DAMAGE_TO_STR_FACTOR = 0.5
NDefines.NNavy.COMBAT_TORPEDO_CRITICAL_CHANCE = 0.25
NDefines.NNavy.COMBAT_TORPEDO_CRITICAL_DAMAGE_MULT = 3.75
NDefines.NNavy.NAVY_PIERCING_THRESHOLDS = { 2.00, 1.50, 1.00, 0.90, 0.75, 0.50, 0.25, 0.10, 0.00 }
NDefines.NNavy.NAVY_PIERCING_THRESHOLD_CRITICAL_VALUES = { 1.75, 1.10, 1.00, 0.90, 0.75, 0.50, 0.25, 0.10, 0.00 }
NDefines.NNavy.NAVY_PIERCING_THRESHOLD_DAMAGE_VALUES = { 1.00, 1.00, 1.00, 0.85, 0.70, 0.40, 0.30, 0.20, 0.10 }
NDefines.NNavy.ALL_SHIPS_ACTIVATE_TIME = 12
NDefines.NNavy.CAPITAL_ONLY_COMBAT_ACTIVATE_TIME = 8

-- Positioning
NDefines.NNavy.MAX_POSITIONING_BONUS_FROM_SURFACE_DETECTION = 0.05
NDefines.NNavy.HIGHER_SHIP_RATIO_POSITIONING_PENALTY_FACTOR = 0.4
NDefines.NNavy.MAX_POSITIONING_PENALTY_FROM_HIGHER_SHIP_RATIO = 1.2
NDefines.NNavy.DAMAGE_PENALTY_ON_MINIMUM_POSITIONING = 0.75
NDefines.NNavy.SCREENING_EFFICIENCY_PENALTY_ON_MINIMUM_POSITIONING = 0.4
NDefines.NNavy.AA_EFFICIENCY_PENALTY_ON_MINIMUM_POSITIONING = 0.4
-- The old code used 99 here, marked as a testing value; it disables the fleet size penalty.
-- NDefines.NNavy.MIN_SHIPS_FOR_HIGHER_SHIP_RATIO_PENALTY = 99

-- Retreat
NDefines.NNavy.COMBAT_RETREAT_DECISION_CHANCE = 0.2
NDefines.NNavy.BASE_ESCAPE_SPEED = 0.06
NDefines.NNavy.SPEED_TO_ESCAPE_SPEED = 1.15
NDefines.NNavy.MAX_ESCAPE_SPEED_FROM_COMBAT_DURATION = 0.20
-- The old code retreated below 40% HP, marked "Testing, normally 4 this is REPAIR_AND_RETURN_PRIO_HIGH_COMBAT".
-- NDefines.NNavy.REPAIR_AND_RETURN_PRIO_MEDIUM_COMBAT = 0.4

-- Experience
NDefines.NNavy.EXPERIENCE_LOSS_FACTOR = 2.0
NDefines.NNavy.UNIT_EXPERIENCE_PER_COMBAT_HOUR = 2
NDefines.NNavy.EXPERIENCE_FACTOR_NON_CARRIER_GAIN = 0.02

-- Air (not simulated until milestone 2)
NDefines.NNavy.BASE_CARRIER_SORTIE_EFFICIENCY = 0.20
NDefines.NNavy.ANTI_AIR_TARGETING = 0.4
NDefines.NNavy.ANTI_AIR_MULT_ON_INCOMING_AIR_DAMAGE = 0.03
NDefines.NNavy.ANTI_AIR_POW_ON_INCOMING_AIR_DAMAGE = 0.58
NDefines.NNavy.NAVAL_COMBAT_AIR_CAPITAL_TARGET_SCALE = 30
NDefines.NNavy.NAVAL_COMBAT_AIR_CARRIER_TARGET_SCALE = 100
NDefines.NAir.CARRIER_HOURS_DELAY_AFTER_EACH_COMBAT = 8
NDefines.NAir.NAVAL_STRIKE_DAMAGE_TO_STR = 1.25
NDefines.NAir.NAVAL_STRIKE_DAMAGE_TO_ORG = 1.25
NDefines.NAir.NAVAL_STRIKE_CARRIER_MULTIPLIER = 1.75
NDefines.NAir.NAVAL_STRIKE_TARGETTING_TO_AMOUNT = 0.4
NDefines.NAir.NAVAL_COMBAT_EXTERNAL_PLANES_JOIN_RATIO = 0.1
NDefines.NAir.NAVAL_COMBAT_EXTERNAL_PLANES_JOIN_RATIO_PER_DAY = 0.25
NDefines.NAir.NAVAL_COMBAT_EXTERNAL_PLANES_MIN_CAP = 30
