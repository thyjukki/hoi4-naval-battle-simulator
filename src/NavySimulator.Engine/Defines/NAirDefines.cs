namespace NavySimulator.Engine.Defines;

/// <summary>
/// Combat-relevant <c>NDefines.NAir</c> values. Property names match the game keys exactly so they can be bound
/// from <c>common/defines/*.lua</c>. Defaults are vanilla 1.19.3 (see docs/mechanics/defines.md for provenance).
/// </summary>
public sealed class NAirDefines
{
    /// <summary>How many hours needs the wing to be ready for the next combat. Use for tweaking if combats happens too often. (generally used as double because of roundtrip)</summary>
    public double HOURS_DELAY_AFTER_EACH_COMBAT { get; set; } = 4.0;

    /// <summary>how often carrier planes do battle inside naval combat</summary>
    public double CARRIER_HOURS_DELAY_AFTER_EACH_COMBAT { get; set; } = 3.0;

    /// <summary>Each Point of carrier_size state adds capacity for this many planes</summary>
    public double CARRIER_SIZE_STAT_INCREMENT { get; set; } = 10.0;

    /// <summary>scales penalty of having overcrowded bases.</summary>
    public double CAPACITY_PENALTY { get; set; } = 2.0;

    /// <summary>Balancing value to convert the naval_strike_targetting equipment stats to chances of how many airplanes managed to do successfull strike.</summary>
    public double NAVAL_STRIKE_TARGETTING_TO_AMOUNT { get; set; } = 0.3;

    /// <summary>Balancing value to convert damage ( naval_strike_attack * hits ) to Strength reduction.</summary>
    public double NAVAL_STRIKE_DAMAGE_TO_STR { get; set; } = 1.0;

    /// <summary>Balancing value to convert damage ( naval_strike_attack * hits ) to Organisation reduction.</summary>
    public double NAVAL_STRIKE_DAMAGE_TO_ORG { get; set; } = 1.5;

    /// <summary>Base chance % for ace pilot die when an airplane is shot down in the Ace wing.</summary>
    public double ACE_DEATH_CHANCE_BASE { get; set; } = 0.005;

    /// <summary>The more airplanes was lost in a single airplanes (more bloody it was) the higher chance of Ace to die.</summary>
    public double ACE_DEATH_CHANCE_PLANES_MULT { get; set; } = 0.001;

    /// <summary>Base chance % for ace pilot creation roll to happen.</summary>
    public double ACE_EARN_CHANCE_BASE { get; set; } = 0.01;

    /// <summary>Ace generation chance per aircraft (A-ACE: per point of HP damage in a naval strike).</summary>
    public double ACE_EARN_CHANCE_PLANES_MULT { get; set; } = 0.005;

    /// <summary>size of wing ace bonuses are set up for. if lower more bonus, if higher less bonus</summary>
    public double ACE_WING_SIZE { get; set; } = 100;

    /// <summary>biggest bonus we can get from having a small wing with an ace on</summary>
    public double ACE_WING_SIZE_MAX_BONUS { get; set; } = 2;

    /// <summary>damage bonus when planes are in naval combat where their carrier is present (and can thus sortie faster and more effectively)</summary>
    public double NAVAL_STRIKE_CARRIER_MULTIPLIER { get; set; } = 12.0;

    /// <summary>conversion factor to bring agility in line with ship AA</summary>
    public double AIR_AGILITY_TO_NAVAL_STRIKE_AGILITY { get; set; } = 0.02;

    /// <summary>Value used to scale the surface_visibility stats to balance the gameplay, so 100% detection chance still won't spam the strikes.</summary>
    public double NAVAL_STRIKE_DETECTION_BALANCE_FACTOR { get; set; } = 0.5;

    /// <summary>Max airbombers to do port strike comparing to strength</summary>
    public double NAVAL_STRIKE_BASE_STR_TO_PLANES_RATIO { get; set; } = 0.03;

    /// <summary>Max planes that can join a combat comparing to the total strength of the ships</summary>
    public double NAVAL_COMBAT_EXTERNAL_PLANES_JOIN_RATIO { get; set; } = 0.05;

    /// <summary>max extra plane % that can join every day</summary>
    public double NAVAL_COMBAT_EXTERNAL_PLANES_JOIN_RATIO_PER_DAY { get; set; } = 0.2;

    /// <summary>Min cap for planes that can join naval combat</summary>
    public double NAVAL_COMBAT_EXTERNAL_PLANES_MIN_CAP { get; set; } = 20.0;

    /// <summary>Anti Air Gun Damage factor</summary>
    public double ANTI_AIR_PLANE_DAMAGE_FACTOR { get; set; } = 0.8;

    /// <summary>Anti Air Gun hit chance</summary>
    public double ANTI_AIR_PLANE_DAMAGE_CHANCE { get; set; } = 0.1;

    /// <summary>Balancing value to convert equipment stat anti_air_attack to the damage reduction modifier apply to incoming air attacks against units with AA.</summary>
    public double ANTI_AIR_ATTACK_TO_DAMAGE_REDUCTION_FACTOR { get; set; } = 2.5;

    /// <summary>Maximum damage reduction factor applied to incoming air attacks against units with AA.</summary>
    public double ANTI_AIR_MAXIMUM_DAMAGE_REDUCTION_FACTOR { get; set; } = 0.75;

    public double COMBAT_DAMAGE_STATS_MULTILPIER { get; set; } = 0.2;

    /// <summary>How much the better agility (than opponent's) can reduce their damage to us.</summary>
    public double COMBAT_BETTER_AGILITY_DAMAGE_REDUCTION { get; set; } = 0.45;

    /// <summary>How much the better Speed (than opponent's) can reduce increase our damage to them.</summary>
    public double COMBAT_BETTER_SPEED_DAMAGE_INCREASE { get; set; } = 0.65;

    /// <summary>How many planes can shoot at each plane on other side ( if there are 100 planes we are atttacking COMBAT_MULTIPLANE_CAP * 100 of our planes can shoot )</summary>
    public double COMBAT_MULTIPLANE_CAP { get; set; } = 3.0;

    /// <summary>Higher value = more shot down planes</summary>
    public double COMBAT_DAMAGE_SCALE { get; set; } = 1.0;

    /// <summary>same as above but used inside naval combat for carrier battles</summary>
    public double COMBAT_DAMAGE_SCALE_CARRIER { get; set; } = 5.0;

    /// <summary>biggest factor difference in agility for doing damage (caps to this)</summary>
    public double BIGGEST_AGILITY_FACTOR_DIFF { get; set; } = 4.0;

    /// <summary>biggest factor difference in speed for doing damage (caps to this)</summary>
    public double BIGGEST_SPEED_FACTOR_DIFF { get; set; } = 3.5;

    /// <summary>A factor for scaling the top speed of a plane into damage buff. If an attacking wing has a speed advantage of any form their speed value will be converted into a percentage bonus with this modifier</summary>
    public double TOP_SPEED_DAMAGE_BONUS_FACTOR { get; set; } = 0.025;

    /// <summary>% how many max disrupted only planes are alloed to die in a single combat</summary>
    public double AIR_COMBAT_FINAL_DAMAGE_SCALE { get; set; } = 0.015;

    /// <summary>scaling/control for when only very few planes exist to stop roundoff issues</summary>
    public double AIR_COMBAT_FINAL_DAMAGE_PLANES { get; set; } = 50.0;

    public double AIR_COMBAT_FINAL_DAMAGE_PLANES_FACTOR { get; set; } = 0.1;

    /// <summary>Max stats</summary>
    public double AIR_WING_MAX_STATS_ATTACK { get; set; } = 100.0;

    public double AIR_WING_MAX_STATS_DEFENCE { get; set; } = 100.0;

    public double AIR_WING_MAX_STATS_AGILITY { get; set; } = 100.0;

    public double AIR_WING_MAX_STATS_SPEED { get; set; } = 800.0;

    /// <summary>Eyeballed average amount of airplanes in wing. Used when calculating air volunteer.</summary>
    public double AIR_WING_AVERAGE_SIZE { get; set; } = 100.0;

    /// <summary>multiplier on disruption damage to scale its effects on planes</summary>
    public double DISRUPTION_FACTOR { get; set; } = 4.0;

    /// <summary>multiplier on disruption damage to scale its effects on carrier vs carrier planes</summary>
    public double DISRUPTION_FACTOR_CARRIER { get; set; } = 6.0;

    public double DISRUPTION_SPEED_FACTOR { get; set; } = 1.0;

    public double DISRUPTION_ATTACK_FACTOR { get; set; } = 0.0;

    public double DISRUPTION_DEFENCE_DEFENCE_FACTOR { get; set; } = 0.5;

    public double DISRUPTION_DEFENCE_SPEED_FACTOR { get; set; } = 1.0;

    public double DISRUPTION_DEFENCE_ATTACK_FACTOR { get; set; } = 0.5;

    public double DISRUPTION_AGILITY_FACTOR { get; set; } = 0.0;

    public double DISRUPTION_DETECTION_FACTOR { get; set; } = 1.0;

    public double ESCORT_FACTOR { get; set; } = 2.0;

    public double ESCORT_SPEED_FACTOR { get; set; } = 1.0;

    public double ESCORT_AGILITY_FACTOR { get; set; } = 1.0;

    public double ESCORT_ATTACK_FACTOR { get; set; } = 1.0;

    /// <summary>below this amount of planes on a carrier we no longer get max benefit on enemy positioning</summary>
    public double CARRIER_PLANES_AMOUNT_FOR_POSITIONING { get; set; } = 50.0;

    public double CARRIER_COMBAT_DAMAGE_STATS_MULTIPLIER { get; set; } = 0.5;

    /// <summary>Percentage of planes able to defend a carrier from air attacks (historically 15% - 35%)</summary>
    public double CARRIER_PERCENTAGE_DEFEND { get; set; } = 0.35;

    /// <summary>Each Point of carrier_size state adds capacity for this many planes for submarines</summary>
    public double SUBMARINE_CARRIER_SIZE_STAT_INCREMENT { get; set; } = 3.0;

    /// <summary>Balancing value to increase usual damage to Strength for Kamikaze</summary>
    public double AIR_NAVAL_KAMIKAZE_DAMAGE_MULT { get; set; } = 20.0;

    /// <summary>Balancing value to increase usual losses if Kamikaze participating in the battle</summary>
    public double AIR_NAVAL_KAMIKAZE_LOSSES_MULT { get; set; } = 4.0;

    /// <summary>Base Kamikaze death rate</summary>
    public double BASE_KAMIKAZE_DAMAGE { get; set; } = 2.0;

    /// <summary>Experience needed to progress to the next level</summary>
    public double[] AIR_WING_XP_LEVELS { get; set; } = [100.0, 300.0, 700.0, 900.0];

    /// <summary>How much the night can reduce the air detection. (see static modifiers to check how weather affects it too.)</summary>
    public double DETECT_CHANCE_FROM_NIGHT { get; set; } = -0.2;

    /// <summary>The max night coverage in a region that is still considered to be day-time when determining if day/night air missions shall run.</summary>
    public double DAY_NIGHT_COVERAGE_FACTOR { get; set; } = 0.5;
}
