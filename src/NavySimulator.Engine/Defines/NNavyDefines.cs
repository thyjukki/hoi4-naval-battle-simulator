namespace NavySimulator.Engine.Defines;

/// <summary>
/// Combat-relevant <c>NDefines.NNavy</c> values. Property names match the game keys exactly so they can be bound
/// from <c>common/defines/*.lua</c>. Defaults are vanilla 1.19.3 (see docs/mechanics/defines.md for provenance).
/// </summary>
public sealed class NNavyDefines
{
    /// <summary>base chance for hit</summary>
    public double COMBAT_BASE_HIT_CHANCE { get; set; } = 0.1;

    /// <summary>never less hit chance then this?</summary>
    public double COMBAT_MIN_HIT_CHANCE { get; set; } = 0.005;

    /// <summary>we take ship evasion stats, and mult by this value, so it gives hit chance reduction. So if reduction is 0.025 and ship evasion = 10, then there will be 0.25 (25%) lower hit chance. (Fe. 50% base -25% from evasion +10% bcoz it's very close).</summary>
    public double COMBAT_EVASION_TO_HIT_CHANCE { get; set; } = 0.007;

    /// <summary>the above evasion hit chance is multiplied by 400% if shooting with torpedoes. Torpedoes are slow, so evasion matters more.</summary>
    public double COMBAT_EVASION_TO_HIT_CHANCE_TORPEDO_MULT { get; set; } = 10.0;

    /// <summary>largest hit profile penalty to hitting</summary>
    public double MIN_HIT_PROFILE_MULT { get; set; } = 0.0;

    /// <summary>% of penalty applied to hit chance when ORG is very low.</summary>
    public double COMBAT_LOW_ORG_HIT_CHANCE_PENALTY { get; set; } = -0.5;

    /// <summary>% of penalty applied to hit chance when manpower is very low.</summary>
    public double COMBAT_LOW_MANPOWER_HIT_CHANCE_PENALTY { get; set; } = -0.25;

    /// <summary>random factor in damage. So if max damage is fe. 10, and randomness is 30%, then damage will be between 7-10.</summary>
    public double COMBAT_DAMAGE_RANDOMNESS { get; set; } = 0.5;

    /// <summary>chance for critical hit from torpedo.</summary>
    public double COMBAT_TORPEDO_CRITICAL_CHANCE { get; set; } = 0.1;

    /// <summary>multiplier to damage when got critical hit from torpedo. (Critical hits are devastating as usualy torpedo_attack are pretty high base values).</summary>
    public double COMBAT_TORPEDO_CRITICAL_DAMAGE_MULT { get; set; } = 2.0;

    /// <summary>casting damage value to ship strength multiplier. Use it ot balance the game difficulty.</summary>
    public double COMBAT_DAMAGE_TO_STR_FACTOR { get; set; } = 0.6;

    /// <summary>casting damage value to ship organisation multiplier. Use it to balance the game difficulty.</summary>
    public double COMBAT_DAMAGE_TO_ORG_FACTOR { get; set; } = 1.0;

    public double NAVY_MAX_XP { get; set; } = 100.0;

    /// <summary>Before we resign chasing enemy, give them some minimum time so the combat doesn't end instantly.</summary>
    public double COMBAT_CHASE_RESIGNATION_HOURS { get; set; } = 8.0;

    /// <summary>Min combat duration before we can retreat. It's a balancing variable so it's not possible to always run with our weak ships agains big flotillas.</summary>
    public double COMBAT_MIN_DURATION { get; set; } = 8.0;

    /// <summary>Number of hours that is considered the "initial phase" of naval combat, used for modifiers like surprise attack during "initial combat"</summary>
    public double COMBAT_INITIAL_DURATION { get; set; } = 6.0;

    /// <summary>There is also random factor in deciding if we should retreat or not. That causes a delay in taking decision, that sooner or later will be picked. It's needed so damaged fast ships won't troll the combat.</summary>
    public double COMBAT_RETREAT_DECISION_CHANCE { get; set; } = 0.2;

    /// <summary>Base chance for receiving a critical chance. It get's scaled down with ship reliability.</summary>
    public double COMBAT_BASE_CRITICAL_CHANCE { get; set; } = 0.05;

    /// <summary>Multiplier for the critical damage. Scaled down with the ship reliability.</summary>
    public double COMBAT_CRITICAL_DAMAGE_MULT { get; set; } = 5.0;

    /// <summary>Bonus to critical chance when shooter armor piercing is higher then target armor.</summary>
    public double COMBAT_ARMOR_PIERCING_CRITICAL_BONUS { get; set; } = 1.0;

    /// <summary>All damage reduction % when target armor is &gt;= then shooter armor piercing. (depricated)</summary>
    public double COMBAT_ARMOR_PIERCING_DAMAGE_REDUCTION { get; set; } = 0.0;

    /// <summary>percentage of experienced solders who die when manpower is removed</summary>
    public double EXPERIENCE_LOSS_FACTOR { get; set; } = 1.0;

    /// <summary>Balancing value to convert averaged equipment stats (anti_air_targetting and naval_strike_agility) to probability chances of airplane being hit by navies AA.</summary>
    public double ANTI_AIR_TARGETTING_TO_CHANCE { get; set; } = 0.2;

    /// <summary>Balancing value to convert equipment stat anti_air_attack to the random % value of airplanes being hit.</summary>
    public double ANTI_AIR_ATTACK_TO_AMOUNT { get; set; } = 0.01;

    /// <summary>how good ships are at hitting aircraft</summary>
    public double ANTI_AIR_TARGETING { get; set; } = 0.9;

    public double UNIT_EXPERIENCE_PER_COMBAT_HOUR { get; set; } = 10.0;

    public double UNIT_EXPERIENCE_SCALE { get; set; } = 1.0;

    public double EXPERIENCE_FACTOR_CONVOY_ATTACK { get; set; } = 0.04;

    /// <summary>Xp gain by non-carrier ships in the combat</summary>
    public double EXPERIENCE_FACTOR_NON_CARRIER_GAIN { get; set; } = 0.04;

    /// <summary>Xp gain by carrier ships in the combat</summary>
    public double EXPERIENCE_FACTOR_CARRIER_GAIN { get; set; } = 0.08;

    /// <summary>factor of planes that can sortie by default from a carrier</summary>
    public double BASE_CARRIER_SORTIE_EFFICIENCY { get; set; } = 0.5;

    /// <summary>sunk ships will lose this ratio of their current manpower</summary>
    public double MANPOWER_LOSS_RATIO_ON_SUNK { get; set; } = 0.5;

    /// <summary>losing strength will make you also lose manpower at this ratio of total manpower</summary>
    public double MANPOWER_LOSS_RATIO_ON_STR_LOSS { get; set; } = 0.5;

    /// <summary>ships will not lose man power to below this ratio</summary>
    public double MIN_MANPOWER_RATIO_TO_DROP { get; set; } = 0.1;

    /// <summary>the ships not in combat will be able to gain this ratio of their max manpower</summary>
    public double DAILY_MANPOWER_GAIN_RATIO { get; set; } = 0.05;

    /// <summary>hours from start of combat when carriers get to fight</summary>
    public double CARRIER_ONLY_COMBAT_ACTIVATE_TIME { get; set; } = 0.0;

    /// <summary>hours from start of combat when only carriers, capitals and subs get to attack</summary>
    public double CAPITAL_ONLY_COMBAT_ACTIVATE_TIME { get; set; } = 6.0;

    /// <summary>hours where all get to attack</summary>
    public double ALL_SHIPS_ACTIVATE_TIME { get; set; } = 8.0;

    /// <summary>% of total Strength. When below, navy will go to home base to repair (in combat).</summary>
    public double REPAIR_AND_RETURN_PRIO_LOW_COMBAT { get; set; } = 0.9;

    /// <summary>% of total Strength. When below, navy will go to home base to repair (in combat).</summary>
    public double REPAIR_AND_RETURN_PRIO_MEDIUM_COMBAT { get; set; } = 0.6;

    /// <summary>% of total Strength. When below, navy will go to home base to repair (in combat).</summary>
    public double REPAIR_AND_RETURN_PRIO_HIGH_COMBAT { get; set; } = 0.4;

    /// <summary>Str below this point is considering a single ship "dying", and a high priority to send to repair.</summary>
    public double REPAIR_AND_RETURN_UNIT_DYING_STR { get; set; } = 0.2;

    /// <summary>daily base escape speed (gained as percentagE)</summary>
    public double BASE_ESCAPE_SPEED { get; set; } = 0.05;

    /// <summary>ratio to converstion from ship speed to escape speed (divided by hundred)</summary>
    public double SPEED_TO_ESCAPE_SPEED { get; set; } = 0.5;

    /// <summary>daily increase in escape speed during combat duration</summary>
    public double ESCAPE_SPEED_PER_COMBAT_DAY { get; set; } = 0.015;

    /// <summary>max escape speed that will be gained from combat duration</summary>
    public double MAX_ESCAPE_SPEED_FROM_COMBAT_DURATION { get; set; } = 0.5;

    /// <summary>subs get faster escape speed. gets replaced by hidden version below if hidden</summary>
    public double ESCAPE_SPEED_SUB_BASE { get; set; } = 0.1;

    /// <summary>hidden subs get faster escape speed</summary>
    public double ESCAPE_SPEED_HIDDEN_SUB { get; set; } = 0.35;

    /// <summary>minimum cooldown for a gun</summary>
    public double MIN_GUN_COOLDOWN { get; set; } = 0.1;

    /// <summary>number of hours for a gun to be ready after shooting</summary>
    public double[] BASE_GUN_COOLDOWNS { get; set; } = [1.0, 4.0, 1.0];

    /// <summary>the taskforces that wants to join existing combats will wait for at least this amount</summary>
    public double BASE_JOIN_COMBAT_HOURS { get; set; } = 2.0;

    /// <summary>base value for positioning</summary>
    public double BASE_POSITIONING { get; set; } = 1.0;

    /// <summary>multiples the surface detection difference between two sides. the side with higher detection will get a bonus of this value</summary>
    public double RELATIVE_SURFACE_DETECTION_TO_POSITIONING_FACTOR { get; set; } = 0.01;

    /// <summary>will clamp the bonus that you get from detection</summary>
    public double MAX_POSITIONING_BONUS_FROM_SURFACE_DETECTION { get; set; } = 0.25;

    /// <summary>if one side has more ships than the other, that side will get this penalty for each +100% ship ratio it has</summary>
    public double HIGHER_SHIP_RATIO_POSITIONING_PENALTY_FACTOR { get; set; } = 0.25;

    /// <summary>maximum penalty to get from larger fleets</summary>
    public double MAX_POSITIONING_PENALTY_FROM_HIGHER_SHIP_RATIO { get; set; } = 0.75;

    /// <summary>the minimum fleet size in ships that a fleet must be before having the large fleet penalty applied to them</summary>
    public double MIN_SHIPS_FOR_HIGHER_SHIP_RATIO_PENALTY { get; set; } = 4.0;

    /// <summary>penalty if other side has stronger carrier air force</summary>
    public double HIGHER_CARRIER_RATIO_POSITIONING_PENALTY_FACTOR { get; set; } = 0.2;

    /// <summary>max penalty from stronger carrier air force</summary>
    public double MAX_CARRIER_RATIO_POSITIONING_PENALTY_FACTOR { get; set; } = 0.2;

    /// <summary>each ship that joins the combat will have this penalty to be added into positioning</summary>
    public double POSITIONING_PENALTY_FOR_SHIPS_JOINED_COMBAT_AFTER_IT_STARTS { get; set; } = 0.01;

    /// <summary>the accumulated penalty from new ships will be clamped to this value</summary>
    public double MAX_POSITIONING_PENALTY_FOR_NEWLY_JOINED_SHIPS { get; set; } = 0.25;

    /// <summary>the accumulated penalty from new ships will decay hourly by this value</summary>
    public double POSITIONING_PENALTY_HOURLY_DECAY_FOR_NEWLY_JOINED_SHIPS { get; set; } = 0.05;

    /// <summary>damage penalty at 0% positioning</summary>
    public double DAMAGE_PENALTY_ON_MINIMUM_POSITIONING { get; set; } = 0.5;

    /// <summary>screening efficiency (screen to capital ratio) at 0% positioning</summary>
    public double SCREENING_EFFICIENCY_PENALTY_ON_MINIMUM_POSITIONING { get; set; } = 0.5;

    /// <summary>AA penalty at 0% positioning</summary>
    public double AA_EFFICIENCY_PENALTY_ON_MINIMUM_POSITIONING { get; set; } = 0.7;

    /// <summary>submarine reveal change on 0% positioning</summary>
    public double SUBMARINE_REVEAL_ON_MINIMUM_POSITIONING { get; set; } = 1.0;

    /// <summary>total sum of fleet's anti air will be multiplied with this ratio and added to calculations anti-air of individual ships while air damage reduction</summary>
    public double SHIP_TO_FLEET_ANTI_AIR_RATIO { get; set; } = 0.25;

    /// <summary>received air damage is calculated using following: 1 - ( (ship_anti_air + fleet_anti_air * SHIP_TO_FLEET_ANTI_AIR_RATIO )^ANTI_AIR_POW_ON_INCOMING_AIR_DAMAGE ) * ANTI_AIR_MULT_ON_INCOMING_AIR_DAMAGE</summary>
    public double ANTI_AIR_POW_ON_INCOMING_AIR_DAMAGE { get; set; } = 0.225;

    public double ANTI_AIR_MULT_ON_INCOMING_AIR_DAMAGE { get; set; } = 0.18;

    /// <summary>damage reduction for incoming air attacks is clamped to this value at maximum.</summary>
    public double MAX_ANTI_AIR_REDUCTION_EFFECT_ON_INCOMING_AIR_DAMAGE { get; set; } = 0.75;

    /// <summary>the game will roll between 0-1 and will damage a random part if below this val on naval critical hits</summary>
    public double CHANCE_TO_DAMAGE_PART_ON_CRITICAL_HIT { get; set; } = 0.1;

    /// <summary>the game will roll between 0-1 and will damage a random part if below this val on air critical hits</summary>
    public double CHANCE_TO_DAMAGE_PART_ON_CRITICAL_HIT_FROM_AIR { get; set; } = 0.1;

    /// <summary>this screen ratio to num capital/carriers is needed for full screening beyond screen line</summary>
    public double SCREEN_RATIO_FOR_FULL_SCREENING_FOR_CAPITALS { get; set; } = 3.0;

    /// <summary>this screen ratio to num convoys is needed for full screening beyond screen line</summary>
    public double SCREEN_RATIO_FOR_FULL_SCREENING_FOR_CONVOYS { get; set; } = 0.5;

    /// <summary>this capital ratio to num carriers is needed for full screening beyond screen line</summary>
    public double CAPITAL_RATIO_FOR_FULL_SCREENING_FOR_CARRIERS { get; set; } = 1.0;

    /// <summary>this capital ratio to num convoys is needed for full screening beyond screen line</summary>
    public double CAPITAL_RATIO_FOR_FULL_SCREENING_FOR_CONVOYS { get; set; } = 0.25;

    /// <summary>hit profiles for guns, if target ih profile is lower the gun will have lower accuracy</summary>
    public double[] GUN_HIT_PROFILES { get; set; } = [70.0, 100.0, 45.0];

    /// <summary>multiplies hit chance of small guns</summary>
    public double DEPTH_CHARGES_HIT_CHANCE_MULT { get; set; } = 1.1;

    /// <summary>multiplies damage of depth charges</summary>
    public double DEPTH_CHARGES_DAMAGE_MULT { get; set; } = 0.7;

    /// <summary>hit profile for depth charges</summary>
    public double DEPTH_CHARGES_HIT_PROFILE { get; set; } = 100.0;

    /// <summary>convoys has this contant hit profile</summary>
    public double CONVOY_HIT_PROFILE { get; set; } = 85.0;

    /// <summary>multiplies hit profile of every ship</summary>
    public double HIT_PROFILE_MULT { get; set; } = 100.0;

    /// <summary>factors speed value when determining it profile (Vis * HIT_PROFILE_MULT * Ship Hit Profile Mult)</summary>
    public double HIT_PROFILE_SPEED_FACTOR { get; set; } = 0.5;

    /// <summary>Base value added to hitprofile speed calulation</summary>
    public double HIT_PROFILE_SPEED_BASE { get; set; } = 20.0;

    /// <summary>balance value for sub detection in combat by ships</summary>
    public double NAVAL_COMBAT_SUB_DETECTION_FACTOR { get; set; } = 1.0;

    /// <summary>Amount of in-game-hours that takes the submarine (with position unrevealed), to hide.</summary>
    public double SUBMARINE_HIDE_TIMEOUT { get; set; } = 8.0;

    /// <summary>Amount of in-game-hours that makes the submarine visible if it is on the defender side.</summary>
    public double SUBMARINE_REVEALED_TIMEOUT { get; set; } = 12.0;

    /// <summary>Base factor for submarine detection. It's modified by the difference of a spotter's submarines detection vs submarine visibility. Use this variable for game balancing. setting this too low will cause bad spotting issues.</summary>
    public double SUBMARINE_REVEAL_BASE_CHANCE { get; set; } = 11.0;

    /// <summary>A scaling factor that is applied to the reveal chance in order to make large differences in detection vs visibility more pronounced</summary>
    public double SUBMARINE_REVEAL_POW { get; set; } = 3.0;

    /// <summary>Chance of a submarine being revealed when it fires. 1.0 is 100%. this chance is then multiplied with modifier created by comparing firer's visibiility and target's detection</summary>
    public double SUBMARINE_BASE_TORPEDO_REVEAL_CHANCE { get; set; } = 0.035;

    public double NAVAL_COMBAT_AIR_SUB_DETECTION_MAX { get; set; } = 10.0;

    /// <summary>lower means sharper curve (ramps up very fast, then flatten out very fast). Must be &gt;0</summary>
    public double NAVAL_COMBAT_AIR_SUB_DETECTION_SLOPE { get; set; } = 10.0;

    /// <summary>Factor applied to the stats of external air planes</summary>
    public double NAVAL_COMBAT_AIR_SUB_DETECTION_EXTERNAL_FACTOR { get; set; } = 1.0;

    /// <summary>Factor of Carrier's sortie efficiency on the stats bellow</summary>
    public double NAVAL_COMBAT_AIR_SUB_DETECTION_INTERNAL_EFFICIENCY_FACTOR { get; set; } = 1.0;

    /// <summary>Factor to apply to the agility of air planes active in a naval combat to deduce their contibution to sub detection</summary>
    public double NAVAL_COMBAT_AIR_AGILITY_TO_SUB_DETECTION { get; set; } = 0.0;

    /// <summary>Same, but for strike attack (aka naval attack)</summary>
    public double NAVAL_COMBAT_AIR_STRIKE_ATTACK_TO_SUB_DETECTION { get; set; } = 0.0;

    /// <summary>Same, but for strike targeting (aka naval targeting)</summary>
    public double NAVAL_COMBAT_AIR_STRIKE_TARGETING_TO_SUB_DETECTION { get; set; } = 0.0;

    /// <summary>Same, but for Max Speed</summary>
    public double NAVAL_COMBAT_AIR_MAX_SPEED_TO_SUB_DETECTION { get; set; } = 0.0;

    /// <summary>Factor applied to the number of active plane in a naval combat to deduce their contribution to sub detection</summary>
    public double NAVAL_COMBAT_AIR_PLANE_COUNT_TO_SUB_DETECTION { get; set; } = 1.0;

    /// <summary>Factor to decay the value of sub detection contributed by planes on the last hour. Note: the maximum value between the decayed value and the newly computed one is taken into account. A decay rate of 1 means that nothing is carried over, the previous value is zerod out. A decay rate of 0 means that the previous value is carried over as is.</summary>
    public double NAVAL_COMBAT_AIR_SUB_DETECTION_DECAY_RATE { get; set; } = 1.0;

    /// <summary>A global factor that applies after all others, right before the sub detection contributed by plane is added to the global sub detection of a combatant</summary>
    public double NAVAL_COMBAT_AIR_SUB_DETECTION_FACTOR { get; set; } = 0.0;

    /// <summary>how much score factor from low health (scales between 0-&gt;this number)</summary>
    public double NAVAL_COMBAT_AIR_STRENGTH_TARGET_SCORE { get; set; } = 5.0;

    /// <summary>how much score factor from low AA guns (scales between 0-&gt;this number)</summary>
    public double NAVAL_COMBAT_AIR_LOW_AA_TARGET_SCORE { get; set; } = 5.0;

    /// <summary>base scoring for target picking for planes inside naval combat based on screening efficency, one define per ship typ</summary>
    public double NAVAL_COMBAT_AIR_SUB_TARGET_BASE { get; set; } = 10.0;

    public double NAVAL_COMBAT_AIR_SCREEN_TARGET_BASE { get; set; } = 10.0;

    public double NAVAL_COMBAT_AIR_CAPITAL_TARGET_BASE { get; set; } = 10.0;

    public double NAVAL_COMBAT_AIR_CARRIER_TARGET_BASE { get; set; } = 10.0;

    public double NAVAL_COMBAT_AIR_CONVOY_TARGET_BASE { get; set; } = 1.0;

    /// <summary>scaled scoring for target picking for planes inside naval combat, max value when zero screening efficency, one define per ship typ</summary>
    public double NAVAL_COMBAT_AIR_SUB_TARGET_SCALE { get; set; } = 10.0;

    public double NAVAL_COMBAT_AIR_SCREEN_TARGET_SCALE { get; set; } = 10.0;

    public double NAVAL_COMBAT_AIR_CAPITAL_TARGET_SCALE { get; set; } = 50.0;

    public double NAVAL_COMBAT_AIR_CARRIER_TARGET_SCALE { get; set; } = 200.0;

    public double NAVAL_COMBAT_AIR_CONVOY_TARGET_SCALE { get; set; } = 1.0;

    /// <summary>Our piercing / their armor must be this value to deal damage fraction equal to the index in the array below [higher number = higher penetration]. If armor is 0, 1.00 will be returned.</summary>
    public double[] NAVY_PIERCING_THRESHOLDS { get; set; } = [2.0, 1.0, 0.75, 0.5, 0.1, 0.0];

    /// <summary>0 armor will always receive maximum damage (so add overmatching at your own peril). the system expects at least 2 values, with no upper limit.</summary>
    public double[] NAVY_PIERCING_THRESHOLD_CRITICAL_VALUES { get; set; } = [2.0, 1.0, 0.75, 0.5, 0.1, 0.0];

    /// <summary>0 armor will always receive maximum damage (so add overmatching at your own peril). the system expects at least 2 values, with no upper limit.</summary>
    public double[] NAVY_PIERCING_THRESHOLD_DAMAGE_VALUES { get; set; } = [1.0, 1.0, 0.7, 0.4, 0.3, 0.1];

    /// <summary>The defensive stance sortie is 1.0 - value in index so their sum equals 1</summary>
    public double[] CARRIER_OFFENSIVE_STANCE_SORTIE_RATIO { get; set; } = [0.0, 0.25, 0.5, 0.75, 1.0];

    /// <summary>The default offensive sortie index in CARRIER_OFFENSIVE_STANCE_SORTIE_RATIO</summary>
    public double CARRIER_OFFENSIVE_STANCE_DEFAULT_INDEX { get; set; } = 2.0;

    /// <summary>Amount of hours from combat start where the selected sortie will override the default one</summary>
    public double SELECTED_SORTIE_INITIAL_TIME { get; set; } = 24.0;

    /// <summary>How many planes flying in a naval combat before penalties are introduced</summary>
    public double NAVAL_COMBAT_PLANE_MIN_STACKING_PENALTY { get; set; } = 80.0;

    /// <summary>Each plane above the optimal amount decreases the amount of airplanes being able to takeoff by such %. Subject to diminishing returns</summary>
    public double NAVAL_COMBAT_PLANE_STACKING_PENALTY_EFFECT { get; set; } = 0.005;

    /// <summary>For dynamic plane efficacy, silhouette value (nominally in planes, but very abstract)</summary>
    public double SHIP_SILHOUETTE_VALUE_PLANES_CAPITAL { get; set; } = 10.0;

    /// <summary>As Above. This one would be nice to split by type, but that's problematic.</summary>
    public double SHIP_SILHOUETTE_VALUE_PLANES_SCREEN { get; set; } = 5.0;

    /// <summary>As Above</summary>
    public double SHIP_SILHOUETTE_VALUE_PLANES_CARRIER { get; set; } = 16.0;

    /// <summary>As Above</summary>
    public double SHIP_SILHOUETTE_VALUE_PLANES_CONVOY { get; set; } = 4.0;

    /// <summary>As Above</summary>
    public double SHIP_SILHOUETTE_VALUE_PLANES_SUBMARINE { get; set; } = 7.0;

    /// <summary>Reduces screen silhouette weight in the naval combat plane cap.</summary>
    public double SCREEN_CAP_REDUCTION_FACTOR { get; set; } = 0.02;

    /// <summary>bonus from air superiority to naval detection.</summary>
    public double DETECTION_CHANCE_MULT_AIR_SUPERIORITY_BONUS { get; set; } = 0.25;

    /// <summary>Multiplier on combat retreat threshold for capital ships and carriers (they can take more punishment)</summary>
    public double CAPITAL_SHIP_COMBAT_RETREAT_MULT { get; set; } = 0.5;

    /// <summary>xp gain factor for navy</summary>
    public double XP_GAIN_FACTOR { get; set; } = 1.0;

    public double SHIP_SILHOUETTE_VALUE_PLANES_SUPPORT { get; set; } = 3.0;

    /// <summary>Reworked sub reveal: sub_visibility is subtracted from this for the divider.</summary>
    public double SUBMARINE_BASE_STEALTH_VALUE { get; set; } = 100.0;

    /// <summary>Reworked sub reveal: multiplies the average submarine detection.</summary>
    public double SUBMARINE_REVEAL_DETECTION_MULTIPLIER { get; set; } = 0.065;

    /// <summary>Reworked sub reveal: applied when the submarine fires torpedoes.</summary>
    public double SUBMARINE_REVEAL_TORPEDO_FIRING_DETECTION_MULTIPLIER { get; set; } = 1.1;

    /// <summary>S-ORG-REGAIN: added to the organization regain factor of the side with naval dominance in the region.</summary>
    public double NAVAL_DOMINANCE_ORG_RECOVERY { get; set; } = 0.1;

    /// <summary>S-DOMINANCE: ship range where there is no penalty nor bonus to naval dominance.</summary>
    public double DOMINANCE_PER_SHIP_PER_RANGE_NEUTRAL { get; set; } = 2000.0;

    /// <summary>S-DOMINANCE: ship speed where there is no penalty nor bonus to naval dominance.</summary>
    public double DOMINANCE_PER_SHIP_PER_SPEED_NEUTRAL { get; set; } = 20.0;

    /// <summary>S-DOMINANCE: bonus to dominance per point of carrier size.</summary>
    public double DOMINANCE_PER_SHIP_PER_CARRIER_SIZE { get; set; } = 0.1;

    /// <summary>S-DOMINANCE: bonus to dominance per point of heavy attack.</summary>
    public double DOMINANCE_PER_SHIP_PER_HEAVY_GUN_ATTACK { get; set; } = 0.01;
}
