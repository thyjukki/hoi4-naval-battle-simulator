namespace NavySimulator.Engine.Defines;

/// <summary>
/// Combat constants that do not come from <c>00_defines.lua</c>. Each value is tagged with where it comes from:
/// [WIKI] the HOI4 wiki (which states the target weights are hard-coded in the executable),
/// [LEGACY] carried over from the previous version of this simulator, source unknown,
/// [ASSUMED] a placeholder chosen for this simulator, to be replaced by reverse-engineering or calibration.
/// See docs/mechanics/README.md for the provenance legend.
/// </summary>
public sealed class HardcodedValues
{
    // [WIKI] Surface target selection base weights by target group: heavy/torpedo weight, then light gun weight.
    public double TargetWeightCapital { get; set; } = 30;
    public double TargetWeightCapitalLight { get; set; } = 2;
    public double TargetWeightScreen { get; set; } = 3;
    public double TargetWeightScreenLight { get; set; } = 6;
    public double TargetWeightCarrier { get; set; } = 15;
    public double TargetWeightCarrierLight { get; set; } = 1;
    public double TargetWeightConvoy { get; set; } = 60;
    public double TargetWeightConvoyLight { get; set; } = 4;
    public double TargetWeightConvoyFromSubmarine { get; set; } = 600;
    public double TargetWeightConvoyFromSubmarineLight { get; set; } = 40;
    public double TargetWeightSubmarine { get; set; } = 4;

    // [WIKI] "Damaged target up to +100%", "escaping target x0.5", "actively fighting target +50%".
    public double TargetWeightMaxDamagedBonus { get; set; } = 1.0;
    public double TargetWeightRetreatingMultiplier { get; set; } = 0.5;
    public double TargetWeightActivelyFightingBonus { get; set; } = 0.5;

    // [WIKI] Full screening gives up to +40% hit chance to the battle line and carrier group.
    public double ScreeningHitChanceBonus { get; set; } = 0.4;

    // [WIKI] Full screening gives up to +20% retreat speed to the battle line and carrier group,
    // full carrier screening another +20% to carriers and convoys.
    public double ScreeningRetreatSpeedBonus { get; set; } = 0.2;
    public double CarrierScreeningRetreatSpeedBonus { get; set; } = 0.2;

    // [LEGACY] Navy experience level thresholds as a fraction of NAVY_MAX_XP, and the damage modifier range across levels.
    public double[] ShipExperienceLevels { get; set; } = [0.1, 0.2, 0.3, 0.4, 0.55, 0.7, 0.9];
    public double ShipExperienceMinDamageFactor { get; set; } = -0.1;
    public double ShipExperienceMaxDamageFactor { get; set; } = 0.15;

    // [ASSUMED] Hit chance multiplier applied at night (time of day in [NightStartHour, NightEndHour)).
    public double NightHitChanceFactor { get; set; } = -0.25;
    public int NightStartHour { get; set; } = 20;
    public int NightEndHour { get; set; } = 6;
}
