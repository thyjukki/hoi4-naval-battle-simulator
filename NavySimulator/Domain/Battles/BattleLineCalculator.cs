namespace NavySimulator.Domain.Battles;

internal static class BattleLineCalculator
{
    public static BattleLines BuildBattleLinesFromFleet(List<Ship> ships)
    {
        var screens = new List<Ship>();
        var capitals = new List<Ship>();
        var carriers = new List<Ship>();
        var submarines = new List<Ship>();
        var convoys = new List<Ship>();

        foreach (var ship in ships)
        {
            if (ship.IsSunk || ship.CurrentStatus == ShipStatus.Retreated)
            {
                continue;
            }

            switch (ship.Design.Hull.Role)
            {
                case ShipRole.Screen:
                    screens.Add(ship);
                    break;
                case ShipRole.Capital:
                    capitals.Add(ship);
                    break;
                case ShipRole.Carrier:
                    carriers.Add(ship);
                    break;
                case ShipRole.Submarine:
                    submarines.Add(ship);
                    break;
                case ShipRole.Convoy:
                    convoys.Add(ship);
                    break;
            }
        }

        return new BattleLines(screens, capitals, carriers, submarines, convoys);
    }

    public static ScreeningSummary CalculateScreening(BattleLines lines, double positioning)
    {
        var contributionFactor = GetPositioningContributionFactor(positioning);

        var requiredScreens =
            Hoi4Defines.SCREEN_RATIO_FOR_FULL_SCREENING_FOR_CAPITALS * (lines.Capitals.Count + lines.Carriers.Count) +
            Hoi4Defines.SCREEN_RATIO_FOR_FULL_SCREENING_FOR_CONVOYS * lines.Convoys.Count;
        var effectiveScreens = lines.Screens.Count * contributionFactor;
        var screeningRatio = requiredScreens <= 0 ? 1.0 : effectiveScreens / requiredScreens;

        var requiredCapitals =
            Hoi4Defines.CAPITAL_RATIO_FOR_FULL_SCREENING_FOR_CARRIERS * lines.Carriers.Count +
            Hoi4Defines.CAPITAL_RATIO_FOR_FULL_SCREENING_FOR_CONVOYS * lines.Convoys.Count;
        var effectiveCapitals = lines.Capitals.Count * contributionFactor;
        var carrierScreeningRatio = requiredCapitals <= 0 ? 1.0 : effectiveCapitals / requiredCapitals;

        return new ScreeningSummary(
            Math.Clamp(screeningRatio, 0, 1),
            Math.Clamp(carrierScreeningRatio, 0, 1));
    }

    public static int GetLineShipCount(BattleLines lines)
    {
        return lines.Screens.Count +
               lines.Capitals.Count +
               lines.Carriers.Count +
               lines.Submarines.Count +
               lines.Convoys.Count;
    }

    public static double CalculateFleetSizePositioning(int ownShipCount, int opponentShipCount)
    {
        if (ownShipCount < Hoi4Defines.MIN_SHIPS_FOR_HIGHER_SHIP_RATIO_PENALTY || ownShipCount <= opponentShipCount)
        {
            return Hoi4Defines.BASE_POSITIONING;
        }

        var shipRatio = ownShipCount / (double)opponentShipCount;
        var ratioAboveParity = Math.Max(0, shipRatio - 1.0);
        var penalty = Math.Min(
            Hoi4Defines.MAX_POSITIONING_PENALTY_FROM_HIGHER_SHIP_RATIO,
            ratioAboveParity * Hoi4Defines.HIGHER_SHIP_RATIO_POSITIONING_PENALTY_FACTOR);

        return Math.Clamp(Hoi4Defines.BASE_POSITIONING - penalty, 0, 1);
    }

    private static double GetPositioningContributionFactor(double positioning)
    {
        // At 0% positioning ships contribute 50%; at 100% they contribute fully.
        return Hoi4Defines.PositioningBaseContribution +
               Hoi4Defines.PositioningContributionScale * Math.Clamp(positioning, 0, 1);
    }
}

