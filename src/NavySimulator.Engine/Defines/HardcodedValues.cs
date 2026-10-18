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

    // [ASSUMED] S-NIGHT: province darkness is 1 from NightStartHour to NightEndHour and 0 otherwise. The game computes
    // darkness per province from the day/night terminator; that model is not decoded.
    public int NightStartHour { get; set; } = 20;
    public int NightEndHour { get; set; } = 6;
}
