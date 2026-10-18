using NavySimulator.Engine.Defines;

namespace NavySimulator.Engine.Battles.Mechanics;

/// <summary>
/// S-SCREEN [RE]: screening and carrier screening efficiency (0x161ccb0). Spec: docs/mechanics/surface.md.
/// </summary>
public static class ScreeningRules
{
    public static ScreeningSummary Calculate(BattleLines lines, double positioning, GameDefines defines)
    {
        var navy = defines.NNavy;
        var factor = ContributionFactor(positioning, defines);

        // Ships in every in-battle state count; submarines do not take part in screening.
        var capitals = lines.Capitals.Count;
        var screens = lines.Screens.Count;
        var carriers = lines.Carriers.Count;
        var convoys = lines.Convoys.Count;

        var screening = 1.0;
        var neededScreens = Math.Ceiling(
            navy.SCREEN_RATIO_FOR_FULL_SCREENING_FOR_CAPITALS * (capitals + carriers) +
            navy.SCREEN_RATIO_FOR_FULL_SCREENING_FOR_CONVOYS * convoys);

        if (neededScreens > 0)
        {
            screening = Math.Clamp(screens / neededScreens * factor, 0, 1);
        }

        var carrierScreening = 1.0;
        var neededCapitals = Math.Ceiling(
            navy.CAPITAL_RATIO_FOR_FULL_SCREENING_FOR_CARRIERS * carriers +
            navy.CAPITAL_RATIO_FOR_FULL_SCREENING_FOR_CONVOYS * convoys);

        if (neededCapitals > 0)
        {
            carrierScreening = Math.Clamp(capitals / neededCapitals * factor, 0, 1);
        }

        return new ScreeningSummary(screening, carrierScreening);
    }

    // [RE] 1 + screening_efficiency modifier (none modelled) - penalty * (1 - positioning), at least 0.01.
    public static double ContributionFactor(double positioning, GameDefines defines)
    {
        var factor = 1.0 - defines.NNavy.SCREENING_EFFICIENCY_PENALTY_ON_MINIMUM_POSITIONING * (1.0 - positioning);
        return Math.Max(factor, defines.Hardcoded.MinScreeningFactor);
    }

    /// <summary>
    /// [RE] Share of the static modifier <c>capital_screening_bonus</c> a carrier-line ship gets (0x161ece0):
    /// 1 - (1 - screening) * (1 - carrierScreening).
    /// </summary>
    public static double CombinedCarrierScreening(ScreeningSummary screening)
    {
        return 1.0 - (1.0 - screening.ScreeningEfficiency) * (1.0 - screening.CarrierScreeningEfficiency);
    }
}
