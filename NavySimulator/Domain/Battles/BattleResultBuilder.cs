using NavySimulator.Domain.Planes;

namespace NavySimulator.Domain.Battles;

internal static class BattleResultBuilder
{
    public static BattleResult BuildResult(
        BattleScenario scenario,
        int hoursElapsed,
        int attackerShipsRemaining,
        int defenderShipsRemaining,
        int attackerShipsRetreated,
        int defenderShipsRetreated,
        int attackerBombersShotDownByShipAa,
        int defenderBombersShotDownByShipAa,
        double attackerCarrierPlaneDamageDealt,
        double defenderCarrierPlaneDamageDealt,
        IReadOnlyDictionary<string, double> attackerPlaneDamageByType,
        IReadOnlyDictionary<string, double> defenderPlaneDamageByType,
        IReadOnlyDictionary<string, int> attackerCarrierSortiesByShipId,
        IReadOnlyDictionary<string, int> defenderCarrierSortiesByShipId,
        IReadOnlyDictionary<string, Dictionary<int, CarrierSortieHourMetrics>> attackerCarrierSortiesByShipIdAndHour,
        IReadOnlyDictionary<string, Dictionary<int, CarrierSortieHourMetrics>> defenderCarrierSortiesByShipIdAndHour,
        int retreatEvents,
        int reengagements,
        List<string> hourlyLog,
        List<ActionResult> allActions)
    {
        var attackerProductionLost = GetSunkProductionCost(scenario.Attacker.Fleet.Ships);
        var defenderProductionLost = GetSunkProductionCost(scenario.Defender.Fleet.Ships);
        var attackerToDefenderProductionLossRatio = SafeRatio(attackerProductionLost, defenderProductionLost);
        var defenderToAttackerProductionLossRatio = SafeRatio(defenderProductionLost, attackerProductionLost);
        var shipReports = BuildShipReports(
            scenario,
            allActions,
            attackerCarrierSortiesByShipId,
            defenderCarrierSortiesByShipId,
            attackerCarrierSortiesByShipIdAndHour,
            defenderCarrierSortiesByShipIdAndHour);
        var attackerPlanesAtStart = GetFleetPlaneStrength(scenario.Attacker.Fleet, _ => true);
        var defenderPlanesAtStart = GetFleetPlaneStrength(scenario.Defender.Fleet, _ => true);
        var attackerPlanesLost = new PlaneStrength(
            0,
            Math.Min(attackerPlanesAtStart.Bombers, attackerBombersShotDownByShipAa));
        var defenderPlanesLost = new PlaneStrength(
            0,
            Math.Min(defenderPlanesAtStart.Bombers, defenderBombersShotDownByShipAa));

        var outcome = "Draw";

        if (defenderProductionLost > attackerProductionLost)
        {
            outcome = "Attacker Victory";
        }
        else if (attackerProductionLost > defenderProductionLost)
        {
            outcome = "Defender Victory";
        }

        return new BattleResult(
            scenario.ID,
            hoursElapsed,
            outcome,
            attackerShipsRemaining,
            defenderShipsRemaining,
            attackerShipsRetreated,
            defenderShipsRetreated,
            attackerProductionLost,
            defenderProductionLost,
            attackerToDefenderProductionLossRatio,
            defenderToAttackerProductionLossRatio,
            attackerPlanesAtStart,
            defenderPlanesAtStart,
            attackerPlanesLost,
            defenderPlanesLost,
            attackerCarrierPlaneDamageDealt,
            defenderCarrierPlaneDamageDealt,
            new Dictionary<string, double>(attackerPlaneDamageByType, StringComparer.Ordinal),
            new Dictionary<string, double>(defenderPlaneDamageByType, StringComparer.Ordinal),
            retreatEvents,
            reengagements,
            hourlyLog,
            shipReports);
    }

    private static PlaneStrength GetFleetPlaneStrength(Fleet fleet, Func<Ship, bool> carrierPredicate)
    {
        var carrierCountByDesign = fleet.Ships
            .Where(ship => ship.Design.Hull.Role == ShipRole.Carrier)
            .Where(carrierPredicate)
            .GroupBy(ship => ship.Design.ID)
            .ToDictionary(group => group.Key, group => group.Count(), StringComparer.Ordinal);

        var fighterPlanes = 0;
        var bomberPlanes = 0;

        foreach (var carrierGroup in carrierCountByDesign)
        {
            if (!fleet.CarrierAirwingsByShipDesign.TryGetValue(carrierGroup.Key, out var assignments))
            {
                continue;
            }

            var carrierCount = carrierGroup.Value;
            fighterPlanes += carrierCount * assignments
                .Where(assignment => assignment.Type == AirwingType.Fighter)
                .Sum(assignment => assignment.PlaneCount);
            bomberPlanes += carrierCount * assignments
                .Where(assignment => assignment.Type == AirwingType.Bomber)
                .Sum(assignment => assignment.PlaneCount);
        }

        return new PlaneStrength(fighterPlanes, bomberPlanes);
    }

    private static double GetSunkProductionCost(List<Ship> ships)
    {
        return ships.Where(ship => ship.IsSunk).Sum(ship => ship.GetFinalStats().ProductionCost);
    }

    private static double SafeRatio(double numerator, double denominator)
    {
        if (denominator <= 0)
        {
            return numerator <= 0 ? 1.0 : double.PositiveInfinity;
        }

        return numerator / denominator;
    }

    private static List<ShipBattleReport> BuildShipReports(
        BattleScenario scenario,
        List<ActionResult> allActions,
        IReadOnlyDictionary<string, int> attackerCarrierSortiesByShipId,
        IReadOnlyDictionary<string, int> defenderCarrierSortiesByShipId,
        IReadOnlyDictionary<string, Dictionary<int, CarrierSortieHourMetrics>> attackerCarrierSortiesByShipIdAndHour,
        IReadOnlyDictionary<string, Dictionary<int, CarrierSortieHourMetrics>> defenderCarrierSortiesByShipIdAndHour)
    {
        var allShips = scenario.Attacker.Fleet.Ships
            .Select(ship => (Ship: ship, Side: "Attacker"))
            .Concat(scenario.Defender.Fleet.Ships.Select(ship => (Ship: ship, Side: "Defender")))
            .OrderBy(entry => entry.Side)
            .ThenBy(entry => entry.Ship.ID)
            .ToList();

        var reports = new List<ShipBattleReport>();

        foreach (var entry in allShips)
        {
            var damagedShips = allActions
                .Where(action => action.ShooterId == entry.Ship.ID && action.Fired && action.Target is not null)
                .Select(action => new ShipDamageReportEntry(
                    action.Hour,
                    action.Target!.ID,
                    action.Weapon,
                    action.DidHit,
                    action.Damage,
                    action.AppliedHpDamage,
                    action.AppliedOrganizationDamage,
                    action.DidKillingBlow,
                    action.DidCriticalHit,
                    action.CriticalDamageMultiplier,
                    action.PiercingValue,
                    action.FinalHitChance,
                    action.DefenderArmor,
                    action.DefenderSpeed,
                    action.DefenderVisibility))
                .ToList();

            var didRetreat = entry.Ship.CurrentStatus == ShipStatus.Retreated;
            var attemptedRetreat = entry.Ship.AttemptedRetreat || didRetreat || entry.Ship.CurrentStatus == ShipStatus.Retreating;
            var attemptedRetreatButSunk = attemptedRetreat && entry.Ship.IsSunk;
            var maxHp = entry.Ship.GetFinalStats().Hp;
            var hpPercentage = maxHp <= 0 ? 0 : entry.Ship.CurrentHP / maxHp;
            var carrierSortiesByShipId = entry.Side == "Attacker" ? attackerCarrierSortiesByShipId : defenderCarrierSortiesByShipId;
            var carrierPlaneSorties = carrierSortiesByShipId.TryGetValue(entry.Ship.ID, out var sorties) ? sorties : 0;
            var carrierSortiesByShipIdAndHour = entry.Side == "Attacker"
                ? attackerCarrierSortiesByShipIdAndHour
                : defenderCarrierSortiesByShipIdAndHour;
            var carrierSortiesByHour = carrierSortiesByShipIdAndHour.TryGetValue(entry.Ship.ID, out var sortiesByHour)
                ? sortiesByHour
                    .OrderBy(sortieByHour => sortieByHour.Key)
                    .Select(sortieByHour => new CarrierSortieReportEntry(
                        sortieByHour.Key,
                        sortieByHour.Value.SortiePlanes,
                        sortieByHour.Value.PlanesLost,
                        sortieByHour.Value.SelectedTargets,
                        sortieByHour.Value.TargetAntiAirDefense,
                        sortieByHour.Value.CombinedFleetAaDamageReduction,
                        sortieByHour.Value.FinalDamageDealt))
                    .ToList()
                : new List<CarrierSortieReportEntry>();

            reports.Add(new ShipBattleReport(
                entry.Ship.ID,
                entry.Side,
                entry.Ship.IsSunk,
                didRetreat,
                attemptedRetreat,
                attemptedRetreatButSunk,
                entry.Ship.CurrentHP,
                maxHp,
                hpPercentage,
                entry.Ship.GetFinalStats().ProductionCost,
                damagedShips.Sum(damageEvent => damageEvent.Damage),
                carrierPlaneSorties,
                carrierSortiesByHour,
                damagedShips));
        }

        return reports;
    }
}

