namespace NavySimulator.Domain.Battles;

public class BattleSimulator
{
    public BattleResult Simulate(BattleScenario scenario)
    {
        var hourlyLog = new List<string>();
        var allActions = new List<ActionResult>();
        var cooldowns = new Dictionary<(string ShipID, WeaponType Weapon), int>();
        var random = new Random(42);
        var attackerBombersShotDownByShipAa = 0;
        var defenderBombersShotDownByShipAa = 0;
        var attackerCarrierPlaneDamageDealt = 0.0;
        var defenderCarrierPlaneDamageDealt = 0.0;
        var attackerCarrierSortiesByShipId = new Dictionary<string, int>(StringComparer.Ordinal);
        var defenderCarrierSortiesByShipId = new Dictionary<string, int>(StringComparer.Ordinal);
        var attackerCarrierSortiesByShipIdAndHour = new Dictionary<string, Dictionary<int, CarrierSortieHourMetrics>>(StringComparer.Ordinal);
        var defenderCarrierSortiesByShipIdAndHour = new Dictionary<string, Dictionary<int, CarrierSortieHourMetrics>>(StringComparer.Ordinal);
        var attackerPlaneDamageByType = new Dictionary<string, double>(StringComparer.Ordinal);
        var defenderPlaneDamageByType = new Dictionary<string, double>(StringComparer.Ordinal);
        var attackerCarrierWingStatesByWingKey = NavalAirCombatSimulator.BuildCarrierWingStatesByWingKey(scenario.Attacker.Fleet);
        var defenderCarrierWingStatesByWingKey = NavalAirCombatSimulator.BuildCarrierWingStatesByWingKey(scenario.Defender.Fleet);
        var attackerFiringOrder = scenario.Attacker.Fleet.Ships.OrderBy(ship => ship.ID, StringComparer.Ordinal).ToList();
        var defenderFiringOrder = scenario.Defender.Fleet.Ships.OrderBy(ship => ship.ID, StringComparer.Ordinal).ToList();

        for (var hour = 1; hour <= scenario.MaxHours; hour++)
        {
            var attackerLines = BattleLineCalculator.BuildBattleLinesFromFleet(scenario.Attacker.Fleet.Ships);
            var defenderLines = BattleLineCalculator.BuildBattleLinesFromFleet(scenario.Defender.Fleet.Ships);

            var attackerShipCount = BattleLineCalculator.GetLineShipCount(attackerLines);
            var defenderShipCount = BattleLineCalculator.GetLineShipCount(defenderLines);
            var attackerPositioning = BattleLineCalculator.CalculateFleetSizePositioning(attackerShipCount, defenderShipCount);
            var defenderPositioning = BattleLineCalculator.CalculateFleetSizePositioning(defenderShipCount, attackerShipCount);

            var attackerScreening = BattleLineCalculator.CalculateScreening(attackerLines, attackerPositioning);
            var defenderScreening = BattleLineCalculator.CalculateScreening(defenderLines, defenderPositioning);
            var attackerAirSortie = NavalAirCombatSimulator.CalculateAirSortieSnapshot(
                scenario,
                scenario.Attacker,
                attackerLines,
                attackerScreening,
                defenderLines,
                attackerCarrierWingStatesByWingKey,
                hour);
            var defenderAirSortie = NavalAirCombatSimulator.CalculateAirSortieSnapshot(
                scenario,
                scenario.Defender,
                defenderLines,
                defenderScreening,
                attackerLines,
                defenderCarrierWingStatesByWingKey,
                hour);
            var attackerAirTargetSelections = NavalAirCombatSimulator.ResolveNavalStrike(defenderLines, attackerAirSortie, random);
            var defenderAirTargetSelections = NavalAirCombatSimulator.ResolveNavalStrike(attackerLines, defenderAirSortie, random);
            NavalAirCombatSimulator.ApplyCarrierWingLosses(attackerCarrierWingStatesByWingKey, attackerAirTargetSelections.CarrierBombersShotDownByWingKey);
            NavalAirCombatSimulator.ApplyCarrierWingLosses(defenderCarrierWingStatesByWingKey, defenderAirTargetSelections.CarrierBombersShotDownByWingKey);
            NavalAirCombatSimulator.AccumulateCarrierSorties(
                attackerAirSortie,
                attackerAirTargetSelections,
                hour,
                attackerCarrierSortiesByShipId,
                attackerCarrierSortiesByShipIdAndHour);
            NavalAirCombatSimulator.AccumulateCarrierSorties(
                defenderAirSortie,
                defenderAirTargetSelections,
                hour,
                defenderCarrierSortiesByShipId,
                defenderCarrierSortiesByShipIdAndHour);
            attackerBombersShotDownByShipAa += attackerAirTargetSelections.BombersShotDown;
            defenderBombersShotDownByShipAa += defenderAirTargetSelections.BombersShotDown;
            attackerCarrierPlaneDamageDealt += attackerAirTargetSelections.CarrierDamageDealt;
            defenderCarrierPlaneDamageDealt += defenderAirTargetSelections.CarrierDamageDealt;
            MergeDamageByType(attackerPlaneDamageByType, attackerAirTargetSelections.DamageByPlaneType);
            MergeDamageByType(defenderPlaneDamageByType, defenderAirTargetSelections.DamageByPlaneType);

            var attackerActions = NavalSurfaceCombatSimulator.ResolveActions(
                attackerFiringOrder,
                defenderLines,
                attackerScreening,
                defenderScreening,
                attackerPositioning,
                hour,
                cooldowns,
                random);
            var defenderActions = NavalSurfaceCombatSimulator.ResolveActions(
                defenderFiringOrder,
                attackerLines,
                defenderScreening,
                attackerScreening,
                defenderPositioning,
                hour,
                cooldowns,
                random);

            NavalSurfaceCombatSimulator.ApplyActionDamage(attackerActions);
            NavalSurfaceCombatSimulator.ApplyActionDamage(defenderActions);
            allActions.AddRange(attackerActions);
            allActions.AddRange(defenderActions);

            var attackerAliveCount = GetAliveShipCount(scenario.Attacker.Fleet.Ships);
            var defenderAliveCount = GetAliveShipCount(scenario.Defender.Fleet.Ships);
            var attackerRetreatedCount = GetRetreatedShipCount(scenario.Attacker.Fleet.Ships);
            var defenderRetreatedCount = GetRetreatedShipCount(scenario.Defender.Fleet.Ships);

            hourlyLog.Add(
                $"Hour {hour}: " +
                $"attacker(screen:{attackerLines.Screens.Count}, capital:{attackerLines.Capitals.Count}, carrier:{attackerLines.Carriers.Count}, sub:{attackerLines.Submarines.Count}) " +
                $"positioning {attackerPositioning:P0}, screenEff {attackerScreening.ScreeningEfficiency:P0}, carrierScreenEff {attackerScreening.CarrierScreeningEfficiency:P0}; " +
                $"defender(screen:{defenderLines.Screens.Count}, capital:{defenderLines.Capitals.Count}, carrier:{defenderLines.Carriers.Count}, sub:{defenderLines.Submarines.Count}) " +
                $"positioning {defenderPositioning:P0}, screenEff {defenderScreening.ScreeningEfficiency:P0}, carrierScreenEff {defenderScreening.CarrierScreeningEfficiency:P0}");
            hourlyLog.Add(
                $"Hour {hour}: attacker damage {NavalSurfaceCombatSimulator.GetTotalDamage(attackerActions):F1}, defender damage {NavalSurfaceCombatSimulator.GetTotalDamage(defenderActions):F1}, " +
                $"attacker ships {attackerAliveCount} (retreated {attackerRetreatedCount}), defender ships {defenderAliveCount} (retreated {defenderRetreatedCount})");

            if (attackerAirSortie.IsSortieHour || defenderAirSortie.IsSortieHour)
            {
                hourlyLog.Add(
                    $"Hour {hour}: air sortie - " +
                    $"attacker carrier {attackerAirSortie.CarrierSortiePlanes}/{attackerAirSortie.CarrierAssignedPlanes} (sortie x{attackerAirSortie.CarrierSortieEfficiencyMultiplier:F2}, night traffic x{attackerAirSortie.CarrierTrafficMultiplier:F2}), " +
                    $"external {attackerAirSortie.ExternalPlanesJoining}/{attackerAirSortie.ExternalEligiblePlanes} (cap {attackerAirSortie.ExternalJoinCap:F1}); " +
                    $"defender carrier {defenderAirSortie.CarrierSortiePlanes}/{defenderAirSortie.CarrierAssignedPlanes} (sortie x{defenderAirSortie.CarrierSortieEfficiencyMultiplier:F2}, night traffic x{defenderAirSortie.CarrierTrafficMultiplier:F2}), " +
                    $"external {defenderAirSortie.ExternalPlanesJoining}/{defenderAirSortie.ExternalEligiblePlanes} (cap {defenderAirSortie.ExternalJoinCap:F1})");
                hourlyLog.Add($"Hour {hour}: air disruption - placeholder disabled (fighter interception not implemented yet)");
                hourlyLog.Add(
                    $"Hour {hour}: air targets - attacker bomber wings {attackerAirSortie.BomberWings}, picks {NavalAirCombatSimulator.FormatAirTargetSelectionSummary(attackerAirTargetSelections)}; " +
                    $"defender bomber wings {defenderAirSortie.BomberWings}, picks {NavalAirCombatSimulator.FormatAirTargetSelectionSummary(defenderAirTargetSelections)}");
                hourlyLog.Add(
                    $"Hour {hour}: air AA preemptive - attacker bombers shot down {attackerAirTargetSelections.BombersShotDown}, " +
                    $"defender bombers shot down {defenderAirTargetSelections.BombersShotDown}");
                hourlyLog.Add(
                    $"Hour {hour}: air strike damage - " +
                    $"attacker HP {attackerAirTargetSelections.TotalDamageDealt:F1}, Org {attackerAirTargetSelections.TotalOrganizationDamageDealt:F1}; " +
                    $"defender HP {defenderAirTargetSelections.TotalDamageDealt:F1}, Org {defenderAirTargetSelections.TotalOrganizationDamageDealt:F1}");
            }

            hourlyLog.Add($"Hour {hour}: attacker actions - {NavalSurfaceCombatSimulator.BuildActionSummary(attackerActions)}");
            hourlyLog.Add($"Hour {hour}: defender actions - {NavalSurfaceCombatSimulator.BuildActionSummary(defenderActions)}");
            hourlyLog.Add($"Hour {hour}: attacker hit summary - {NavalSurfaceCombatSimulator.BuildHitSummary(attackerActions)}");
            hourlyLog.Add($"Hour {hour}: defender hit summary - {NavalSurfaceCombatSimulator.BuildHitSummary(defenderActions)}");
            hourlyLog.Add($"Hour {hour}: attacker skips - {NavalSurfaceCombatSimulator.BuildSkipSummary(attackerActions)}");
            hourlyLog.Add($"Hour {hour}: defender skips - {NavalSurfaceCombatSimulator.BuildSkipSummary(defenderActions)}");

            if (attackerAliveCount == attackerRetreatedCount || defenderAliveCount == defenderRetreatedCount)
            {
                hourlyLog.Add($"Hour {hour}: Battle ended since one side has retreated");
                return BattleResultBuilder.BuildResult(
                    scenario,
                    hour,
                    attackerAliveCount,
                    defenderAliveCount,
                    attackerRetreatedCount,
                    defenderRetreatedCount,
                    attackerBombersShotDownByShipAa,
                    defenderBombersShotDownByShipAa,
                    attackerCarrierPlaneDamageDealt,
                    defenderCarrierPlaneDamageDealt,
                    attackerPlaneDamageByType,
                    defenderPlaneDamageByType,
                    attackerCarrierSortiesByShipId,
                    defenderCarrierSortiesByShipId,
                    attackerCarrierSortiesByShipIdAndHour,
                    defenderCarrierSortiesByShipIdAndHour,
                    hourlyLog,
                    allActions);
            }

            if (attackerAliveCount == 0 || defenderAliveCount == 0)
            {
                return BattleResultBuilder.BuildResult(
                    scenario,
                    hour,
                    attackerAliveCount,
                    defenderAliveCount,
                    attackerRetreatedCount,
                    defenderRetreatedCount,
                    attackerBombersShotDownByShipAa,
                    defenderBombersShotDownByShipAa,
                    attackerCarrierPlaneDamageDealt,
                    defenderCarrierPlaneDamageDealt,
                    attackerPlaneDamageByType,
                    defenderPlaneDamageByType,
                    attackerCarrierSortiesByShipId,
                    defenderCarrierSortiesByShipId,
                    attackerCarrierSortiesByShipIdAndHour,
                    defenderCarrierSortiesByShipIdAndHour,
                    hourlyLog,
                    allActions);
            }
        }

        var finalAttackerAlive = GetAliveShipCount(scenario.Attacker.Fleet.Ships);
        var finalDefenderAlive = GetAliveShipCount(scenario.Defender.Fleet.Ships);
        var finalAttackerRetreated = GetRetreatedShipCount(scenario.Attacker.Fleet.Ships);
        var finalDefenderRetreated = GetRetreatedShipCount(scenario.Defender.Fleet.Ships);

        return BattleResultBuilder.BuildResult(
            scenario,
            scenario.MaxHours,
            finalAttackerAlive,
            finalDefenderAlive,
            finalAttackerRetreated,
            finalDefenderRetreated,
            attackerBombersShotDownByShipAa,
            defenderBombersShotDownByShipAa,
            attackerCarrierPlaneDamageDealt,
            defenderCarrierPlaneDamageDealt,
            attackerPlaneDamageByType,
            defenderPlaneDamageByType,
            attackerCarrierSortiesByShipId,
            defenderCarrierSortiesByShipId,
            attackerCarrierSortiesByShipIdAndHour,
            defenderCarrierSortiesByShipIdAndHour,
            hourlyLog,
            allActions);
    }

    private static int GetAliveShipCount(List<Ship> ships)
    {
        var count = 0;

        foreach (var ship in ships)
        {
            if (!ship.IsSunk)
            {
                count++;
            }
        }

        return count;
    }

    private static int GetRetreatedShipCount(List<Ship> ships)
    {
        var count = 0;

        foreach (var ship in ships)
        {
            if (ship.CurrentStatus == ShipStatus.Retreated)
            {
                count++;
            }
        }

        return count;
    }

    private static void MergeDamageByType(
        Dictionary<string, double> aggregate,
        IReadOnlyDictionary<string, double> additions)
    {
        foreach (var kvp in additions)
        {
            aggregate[kvp.Key] = aggregate.TryGetValue(kvp.Key, out var current)
                ? current + kvp.Value
                : kvp.Value;
        }
    }

}
