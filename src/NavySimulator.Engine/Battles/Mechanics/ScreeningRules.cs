using NavySimulator.Engine.Defines;

namespace NavySimulator.Engine.Battles.Mechanics;

/// <summary>
/// S-SCREEN: screening and carrier screening efficiency. Spec: docs/mechanics/surface.md.
/// </summary>
public static class ScreeningRules
{
    public static ScreeningSummary Calculate(BattleLines lines, double positioning, GameDefines defines)
    {
        var navy = defines.NNavy;
        var contribution = PositioningRules.ScreeningContributionFactor(positioning, defines);

        var requiredScreens =
            navy.SCREEN_RATIO_FOR_FULL_SCREENING_FOR_CAPITALS * (lines.Capitals.Count + lines.Carriers.Count) +
            navy.SCREEN_RATIO_FOR_FULL_SCREENING_FOR_CONVOYS * lines.Convoys.Count;
        var screeningRatio = requiredScreens <= 0 ? 1.0 : lines.Screens.Count * contribution / requiredScreens;

        var requiredCapitals =
            navy.CAPITAL_RATIO_FOR_FULL_SCREENING_FOR_CARRIERS * lines.Carriers.Count +
            navy.CAPITAL_RATIO_FOR_FULL_SCREENING_FOR_CONVOYS * lines.Convoys.Count;
        var carrierScreeningRatio = requiredCapitals <= 0 ? 1.0 : lines.Capitals.Count * contribution / requiredCapitals;

        return new ScreeningSummary(Math.Clamp(screeningRatio, 0, 1), Math.Clamp(carrierScreeningRatio, 0, 1));
    }
}
