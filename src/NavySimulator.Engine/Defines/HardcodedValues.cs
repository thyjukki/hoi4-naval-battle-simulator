namespace NavySimulator.Engine.Defines;

/// <summary>
/// Combat constants that are not in any defines or script file. Each value is tagged:
/// [RE] read from hoi4.exe 1.19.3 (docs/reverse-engineering/findings), [ASSUMED] a placeholder for something not yet decoded.
/// See docs/mechanics/README.md for the provenance legend.
/// </summary>
public sealed class HardcodedValues
{
    // [RE] S-TARGET-WEIGHT (0x1971440): base weight by target type, x15 for heavy/torpedo against capitals, carriers and
    // convoys, x2 for the light slot against screens; convoy base is 40 when the shooter is a submarine.
    public double TargetBaseCapital { get; set; } = 2;
    public double TargetBaseScreen { get; set; } = 3;
    public double TargetBaseSubmarine { get; set; } = 4;
    public double TargetBaseOther { get; set; } = 1;
    public double TargetBaseConvoy { get; set; } = 4;
    public double TargetBaseConvoyFromSubmarine { get; set; } = 40;
    public double TargetHeavyTorpedoMultiplier { get; set; } = 15;
    public double TargetLightScreenMultiplier { get; set; } = 2;

    // [RE] Damaged targets: ships x(2 - hpRatio), convoys x(1 + 10 * missing ratio).
    public double TargetConvoyDamagedFactor { get; set; } = 10;

    // [RE] Target state multiplier (exclusive): fighting x1.5, retreating x0.5, waiting x1.
    public double TargetFightingMultiplier { get; set; } = 1.5;
    public double TargetRetreatingMultiplier { get; set; } = 0.5;

    // [RE] S-POS-DETECT: the summed surface detection difference is divided by this before the define factor.
    public double DetectionPositioningDivisor { get; set; } = 100;

    // [RE] S-SCREEN: lower bound of the screening contribution factor.
    public double MinScreeningFactor { get; set; } = 0.01;

    // [RE] S-HIT-SHIP: lower bound of the speed term before HIT_PROFILE_SPEED_BASE is added.
    public double MinHitProfileSpeedTerm { get; set; } = 0.1;

    // [RE] S-SUB: lower bound of (SUBMARINE_BASE_STEALTH_VALUE - visibility) in the reveal chance.
    public double MinSubmarineStealthDivider { get; set; } = 0.1;

    // [RE] S-STATS-COMBAT 0xc3a740: lower bound of the combat speed factor 1 + naval_speed_factor.
    public double MinCombatSpeedFactor { get; set; } = 0.1;

    // [RE] S-STATS-COMBAT 0xc3d4a3: lower bound of the maximum organization.
    public double MinMaxOrganization { get; set; } = 0.001;

    // [RE] S-ADMIRAL-MOD 0x60fbd0: the leader efficiency scales an admiral's modifier by the modifier definition's flags
    // (registration 0x55f7f0, def+0x60). These "positive is bad" keys are scaled only where negative (a benefit); every
    // other naval combat key is "positive is good" and scaled whatever its sign. "Neutral" keys, never scaled, have no
    // naval combat reader.
    public HashSet<string> LeaderModifiersScaledWhenNegative { get; set; } = new(
    [
        "navy_visibility", "naval_critical_effect_factor", "critical_receive_chance", "naval_torpedo_reveal_chance_factor",
        "naval_torpedo_cooldown_factor", "naval_torpedo_enemy_critical_chance_factor", "carrier_capacity_penalty_reduction",
        "navy_casualty_on_hit", "navy_casualty_on_sink", "naval_enemy_retreat_chance", "reassignment_duration_factor"
    ], StringComparer.Ordinal);

    // [ASSUMED] S-NIGHT: without a battle province, darkness is 1 from NightStartHour to NightEndHour and 0 otherwise.
    // With one, the decoded terminator below is used.
    public int NightStartHour { get; set; } = 20;
    public int NightEndHour { get; set; } = 6;

    // [RE] S-NIGHT 0xf15eb0: the sun's tilt runs from −SunTiltDegrees at the summer solstice (day of year 172, 22 June) to
    // +SunTiltDegrees at the winter solstice (355, 22 December), linearly, over 365-day years.
    public int SummerSolsticeDay { get; set; } = 172;
    public int WinterSolsticeDay { get; set; } = 355;
    public int DaysPerYear { get; set; } = 365;
    public double SunTiltDegrees { get; set; } = 5;

    // [RE] S-NIGHT 0xf14fe0: the hour angle is DarknessDayAngle (0x9965e, one fx5 step below the 2π of sin/cos) × the
    // fraction of the day.
    public double DarknessDayAngle { get; set; } = 6.28318;

    // [RE] A-WINGS-TYPES: the sub-unit type keywords of the fighter-type test 0xf63100 (flags & 0x100000c00: fighter,
    // interceptor, heavy_fighter), which picks the defending carrier fighters (A-INTERCEPT), and of the sortie test
    // (& 0x100000400: fighter, heavy_fighter; A-SORTIE). Both read the wing pool's sub-unit type, not the equipment's.
    public HashSet<string> FighterSubUnitTypes { get; set; } = new(["fighter", "interceptor", "heavy_fighter"], StringComparer.Ordinal);
    public HashSet<string> SortieFighterSubUnitTypes { get; set; } = new(["fighter", "heavy_fighter"], StringComparer.Ordinal);

    // [RE] A-WINGS-STRIKE: a wing is in its air object's strike list when its available missions (the plane's
    // allow_mission_type) have naval strike, naval kamikaze or port strike (& 0x190).
    public HashSet<string> StrikeListMissions { get; set; } = new(["naval_bomber", "naval_kamikaze", "port_strike"], StringComparer.Ordinal);

    // [RE] A-ACE-ROLLS (0xf80f60, imul 0x2710): the per-plane (per HP) terms of the ace earn and death chances are scaled by 0.1.
    public double AceChancePlanesScale { get; set; } = 0.1;

    // [RE] A-REGION (0xf862a0, imul 0x3e8): the region air war's losses are trunc(scale × damage) × 0.01 / defence.
    public double AirCombatLossScale { get; set; } = 0.01;

    // [RE] A-REGION-DISRUPT (0xf78160, divisor 316200): e = 1 − sqrt(disruption / den) × 0.316228.
    public double DisruptionEfficiencyScale { get; set; } = 0.316228;

    // [RE] A-ACCIDENT 0xc60290: an air base entry has an accident pass in an hour when its first draw (of 100,000) is below this.
    public int AirAccidentGate { get; set; } = 10000;

    // [RE + LOG] A-STATS-XP 0xf6602d (× 1e6 / 1e10 as an integer): a wing's experience share v is truncated toward zero
    // to steps of this size before it scales the experience bonus or malus.
    public double AirWingExperienceStep { get; set; } = 0.1;

    // [RE] A-DETECT 0xf77730: the floor of a country's air detection in a region (0x3e8 in fixed point).
    public double MinimumRegionAirDetection { get; set; } = 0.01;

    // [RE] A-WINGS-STRIKE: only strike wings executing naval strike or naval kamikaze (& 0x90) enter the strike averages
    // and take losses; a wing can execute only a mission its plane allows.
    public HashSet<string> NavalStrikeMissions { get; set; } = new(["naval_bomber", "naval_kamikaze"], StringComparer.Ordinal);
}
