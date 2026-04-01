namespace NavySimulator.Domain.Battles;

public class BattleSimulator
{
    internal sealed class SimulationState
    {
        public readonly List<string> HourlyLog = [];
        public readonly List<ActionResult> AllActions = [];
        public readonly Random Random;
        public readonly Dictionary<string, int> AttackerCarrierSortiesByShipId = new(StringComparer.Ordinal);
        public readonly Dictionary<string, int> DefenderCarrierSortiesByShipId = new(StringComparer.Ordinal);
        public readonly Dictionary<string, Dictionary<int, CarrierSortieHourMetrics>> AttackerCarrierSortiesByShipIdAndHour = new(StringComparer.Ordinal);
        public readonly Dictionary<string, Dictionary<int, CarrierSortieHourMetrics>> DefenderCarrierSortiesByShipIdAndHour = new(StringComparer.Ordinal);
        public readonly Dictionary<string, double> AttackerPlaneDamageByType = new(StringComparer.Ordinal);
        public readonly Dictionary<string, double> DefenderPlaneDamageByType = new(StringComparer.Ordinal);
        public readonly IReadOnlyDictionary<string, CarrierWingState> AttackerCarrierWingStatesByWingKey;
        public readonly IReadOnlyDictionary<string, CarrierWingState> DefenderCarrierWingStatesByWingKey;
        public readonly List<Ship> AttackerFiringOrder;
        public readonly List<Ship> DefenderFiringOrder;

        public int AttackerBombersShotDownByShipAa;
        public int DefenderBombersShotDownByShipAa;
        public double AttackerCarrierPlaneDamageDealt;
        public double DefenderCarrierPlaneDamageDealt;
        public int RetreatEvents;
        public int Reengagements;

        public SimulationState(BattleScenario scenario, NavalAirCombatSimulator navalAirCombatSimulator, int seed)
        {
            Random = new Random(seed);
            AttackerCarrierWingStatesByWingKey = navalAirCombatSimulator.BuildCarrierWingStatesByWingKey(scenario.Attacker.Fleet);
            DefenderCarrierWingStatesByWingKey = navalAirCombatSimulator.BuildCarrierWingStatesByWingKey(scenario.Defender.Fleet);
            AttackerFiringOrder = scenario.Attacker.Fleet.Ships.OrderBy(ship => ship.ID, StringComparer.Ordinal).ToList();
            DefenderFiringOrder = scenario.Defender.Fleet.Ships.OrderBy(ship => ship.ID, StringComparer.Ordinal).ToList();
        }
    }

    internal readonly record struct HourSimulationResult(
        int AttackerAliveCount,
        int DefenderAliveCount,
        int AttackerRetreatedCount,
        int DefenderRetreatedCount,
        bool ContinueSimulation);

    internal readonly record struct AirPhaseSnapshot(
        AirSortieSnapshot AttackerSortie,
        AirSortieSnapshot DefenderSortie,
        NavalStrikeSelectionSummary AttackerSelections,
        NavalStrikeSelectionSummary DefenderSelections);

    internal readonly record struct SurfacePhaseSnapshot(
        List<ActionResult> AttackerActions,
        List<ActionResult> DefenderActions,
        int AttackerAliveCount,
        int DefenderAliveCount,
        int AttackerRetreatedCount,
        int DefenderRetreatedCount);

    private readonly NavalAirCombatSimulator _navalAirCombatSimulator;

    public BattleSimulator()
        : this(new NavalAirCombatSimulator())
    {
    }

    internal BattleSimulator(NavalAirCombatSimulator navalAirCombatSimulator)
    {
        _navalAirCombatSimulator = navalAirCombatSimulator;
    }

    public BattleResult Simulate(BattleScenario scenario, int? seedOverride = null)
    {
        var seed = seedOverride ?? scenario.Seed ?? Random.Shared.Next();
        var state = new SimulationState(scenario, _navalAirCombatSimulator, seed);

        for (var hour = 1; hour <= scenario.MaxHours; hour++)
        {
            var hourResult = SimulateHour(scenario, hour, state);

            if (!hourResult.ContinueSimulation)
            {
                return BattleResultBuilder.BuildResult(
                    scenario,
                    hour,
                    hourResult.AttackerAliveCount,
                    hourResult.DefenderAliveCount,
                    hourResult.AttackerRetreatedCount,
                    hourResult.DefenderRetreatedCount,
                    state.AttackerBombersShotDownByShipAa,
                    state.DefenderBombersShotDownByShipAa,
                    state.AttackerCarrierPlaneDamageDealt,
                    state.DefenderCarrierPlaneDamageDealt,
                    state.AttackerPlaneDamageByType,
                    state.DefenderPlaneDamageByType,
                    state.AttackerCarrierSortiesByShipId,
                    state.DefenderCarrierSortiesByShipId,
                    state.AttackerCarrierSortiesByShipIdAndHour,
                    state.DefenderCarrierSortiesByShipIdAndHour,
                    state.RetreatEvents,
                    state.Reengagements,
                    state.HourlyLog,
                    state.AllActions);
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
            state.AttackerBombersShotDownByShipAa,
            state.DefenderBombersShotDownByShipAa,
            state.AttackerCarrierPlaneDamageDealt,
            state.DefenderCarrierPlaneDamageDealt,
            state.AttackerPlaneDamageByType,
            state.DefenderPlaneDamageByType,
            state.AttackerCarrierSortiesByShipId,
            state.DefenderCarrierSortiesByShipId,
            state.AttackerCarrierSortiesByShipIdAndHour,
            state.DefenderCarrierSortiesByShipIdAndHour,
            state.RetreatEvents,
            state.Reengagements,
            state.HourlyLog,
            state.AllActions);
    }

    private HourSimulationResult SimulateHour(BattleScenario scenario, int hour, SimulationState state)
    {
        var attackerLines = BattleLineCalculator.BuildBattleLinesFromFleet(scenario.Attacker.Fleet.Ships);
        var defenderLines = BattleLineCalculator.BuildBattleLinesFromFleet(scenario.Defender.Fleet.Ships);

        var attackerShipCount = BattleLineCalculator.GetLineShipCount(attackerLines);
        var defenderShipCount = BattleLineCalculator.GetLineShipCount(defenderLines);
        var attackerPositioning = BattleLineCalculator.CalculateFleetSizePositioning(attackerShipCount, defenderShipCount);
        var defenderPositioning = BattleLineCalculator.CalculateFleetSizePositioning(defenderShipCount, attackerShipCount);

        var attackerScreening = BattleLineCalculator.CalculateScreening(attackerLines, attackerPositioning);
        var defenderScreening = BattleLineCalculator.CalculateScreening(defenderLines, defenderPositioning);
        var airPhase = ResolveAirPhase(
            scenario,
            hour,
            state,
            attackerLines,
            defenderLines,
            attackerScreening,
            defenderScreening);
        var surfacePhase = ResolveSurfacePhase(
            scenario,
            hour,
            state,
            attackerLines,
            defenderLines,
            attackerScreening,
            defenderScreening,
            attackerPositioning,
            defenderPositioning);

        AppendHourlyLogs(
            hour,
            state,
            attackerLines,
            defenderLines,
            attackerPositioning,
            defenderPositioning,
            attackerScreening,
            defenderScreening,
            airPhase,
            surfacePhase);

        if (hour % 24 == 0)
        {
            ApplyDailyManpowerRecovery(scenario.Attacker.Fleet.Ships);
            ApplyDailyManpowerRecovery(scenario.Defender.Fleet.Ships);
        }

        return EvaluateHourTermination(scenario, hour, state, surfacePhase);
    }

    private static void ApplyDailyManpowerRecovery(List<Ship> ships)
    {
        foreach (var ship in ships)
        {
            if (ship.IsSunk)
            {
                continue;
            }

            ship.ApplyDailyManpowerRecovery();
        }
    }

    private AirPhaseSnapshot ResolveAirPhase(
        BattleScenario scenario,
        int hour,
        SimulationState state,
        BattleLines attackerLines,
        BattleLines defenderLines,
        ScreeningSummary attackerScreening,
        ScreeningSummary defenderScreening)
    {
        var attackerSortie = _navalAirCombatSimulator.CalculateAirSortieSnapshot(
            scenario,
            scenario.Attacker,
            attackerLines,
            attackerScreening,
            defenderLines,
            state.AttackerCarrierWingStatesByWingKey,
            hour);
        var defenderSortie = _navalAirCombatSimulator.CalculateAirSortieSnapshot(
            scenario,
            scenario.Defender,
            defenderLines,
            defenderScreening,
            attackerLines,
            state.DefenderCarrierWingStatesByWingKey,
            hour);

        var attackerSelections = _navalAirCombatSimulator.ResolveNavalStrike(defenderLines, attackerSortie, state.Random);
        var defenderSelections = _navalAirCombatSimulator.ResolveNavalStrike(attackerLines, defenderSortie, state.Random);

        _navalAirCombatSimulator.ApplyCarrierWingLosses(state.AttackerCarrierWingStatesByWingKey, attackerSelections.CarrierBombersShotDownByWingKey);
        _navalAirCombatSimulator.ApplyCarrierWingLosses(state.DefenderCarrierWingStatesByWingKey, defenderSelections.CarrierBombersShotDownByWingKey);
        _navalAirCombatSimulator.AccumulateCarrierSorties(
            attackerSortie,
            attackerSelections,
            hour,
            state.AttackerCarrierSortiesByShipId,
            state.AttackerCarrierSortiesByShipIdAndHour);
        _navalAirCombatSimulator.AccumulateCarrierSorties(
            defenderSortie,
            defenderSelections,
            hour,
            state.DefenderCarrierSortiesByShipId,
            state.DefenderCarrierSortiesByShipIdAndHour);

        state.AttackerBombersShotDownByShipAa += attackerSelections.BombersShotDown;
        state.DefenderBombersShotDownByShipAa += defenderSelections.BombersShotDown;
        state.AttackerCarrierPlaneDamageDealt += attackerSelections.CarrierDamageDealt;
        state.DefenderCarrierPlaneDamageDealt += defenderSelections.CarrierDamageDealt;
        MergeDamageByType(state.AttackerPlaneDamageByType, attackerSelections.DamageByPlaneType);
        MergeDamageByType(state.DefenderPlaneDamageByType, defenderSelections.DamageByPlaneType);

        return new AirPhaseSnapshot(attackerSortie, defenderSortie, attackerSelections, defenderSelections);
    }

    private SurfacePhaseSnapshot ResolveSurfacePhase(
        BattleScenario scenario,
        int hour,
        SimulationState state,
        BattleLines attackerLines,
        BattleLines defenderLines,
        ScreeningSummary attackerScreening,
        ScreeningSummary defenderScreening,
        double attackerPositioning,
        double defenderPositioning)
    {
        var battleHasCarriers = attackerLines.Carriers.Count > 0 || defenderLines.Carriers.Count > 0;
        var battleHasCapitals = attackerLines.Capitals.Count > 0 || defenderLines.Capitals.Count > 0;

        var attackerActions = NavalSurfaceCombatSimulator.ResolveActions(
            state.AttackerFiringOrder,
            defenderLines,
            attackerScreening,
            defenderScreening,
            attackerPositioning,
            battleHasCarriers,
            battleHasCapitals,
            scenario.DontRetreat,
            hour,
            state.Random,
            out var attackerRetreatEvents);
        var defenderActions = NavalSurfaceCombatSimulator.ResolveActions(
            state.DefenderFiringOrder,
            attackerLines,
            defenderScreening,
            attackerScreening,
            defenderPositioning,
            battleHasCarriers,
            battleHasCapitals,
            scenario.DontRetreat,
            hour,
            state.Random,
            out var defenderRetreatEvents);
        state.RetreatEvents += attackerRetreatEvents + defenderRetreatEvents;
        state.AllActions.AddRange(attackerActions);
        state.AllActions.AddRange(defenderActions);

        var attackerAliveCount = GetAliveShipCount(scenario.Attacker.Fleet.Ships);
        var defenderAliveCount = GetAliveShipCount(scenario.Defender.Fleet.Ships);
        var attackerRetreatedCount = GetRetreatedShipCount(scenario.Attacker.Fleet.Ships);
        var defenderRetreatedCount = GetRetreatedShipCount(scenario.Defender.Fleet.Ships);

        ApplyCombatExperienceGainForSide(
            attackerActions,
            scenario.Attacker.Fleet.Ships,
            defenderAliveCount,
            attackerAliveCount);
        ApplyCombatExperienceGainForSide(
            defenderActions,
            scenario.Defender.Fleet.Ships,
            attackerAliveCount,
            defenderAliveCount);

        return new SurfacePhaseSnapshot(
            attackerActions,
            defenderActions,
            attackerAliveCount,
            defenderAliveCount,
            attackerRetreatedCount,
            defenderRetreatedCount);
    }

    private static void ApplyCombatExperienceGainForSide(
        List<ActionResult> sideActions,
        List<Ship> ownShips,
        int enemyShipCount,
        int ownShipCount)
    {
        if (ownShipCount <= 0)
        {
            return;
        }

        var firedShipIds = sideActions
            .Where(action => action.Fired)
            .Select(action => action.ShooterId)
            .ToHashSet(StringComparer.Ordinal);

        if (firedShipIds.Count == 0)
        {
            return;
        }

        var experienceGain =
            Hoi4Defines.UNIT_EXPERIENCE_PER_COMBAT_HOUR *
            Hoi4Defines.UNIT_EXPERIENCE_SCALE *
            Hoi4Defines.EXPERIENCE_FACTOR_NON_CARRIER_GAIN *
            enemyShipCount /
            ownShipCount;

        foreach (var ship in ownShips)
        {
            if (!firedShipIds.Contains(ship.ID))
            {
                continue;
            }

            ship.Experience += experienceGain;
        }
    }

    private void AppendHourlyLogs(
        int hour,
        SimulationState state,
        BattleLines attackerLines,
        BattleLines defenderLines,
        double attackerPositioning,
        double defenderPositioning,
        ScreeningSummary attackerScreening,
        ScreeningSummary defenderScreening,
        AirPhaseSnapshot airPhase,
        SurfacePhaseSnapshot surfacePhase)
    {
        AppendPositioningAndFleetStateLog(
            hour,
            state,
            attackerLines,
            defenderLines,
            attackerPositioning,
            defenderPositioning,
            attackerScreening,
            defenderScreening,
            surfacePhase);
        AppendAirPhaseLogs(hour, state, airPhase);
        AppendSurfacePhaseLogs(hour, state, surfacePhase);
    }

    private static void AppendPositioningAndFleetStateLog(
        int hour,
        SimulationState state,
        BattleLines attackerLines,
        BattleLines defenderLines,
        double attackerPositioning,
        double defenderPositioning,
        ScreeningSummary attackerScreening,
        ScreeningSummary defenderScreening,
        SurfacePhaseSnapshot surfacePhase)
    {
        state.HourlyLog.Add(
            $"Hour {hour}: " +
            $"attacker(screen:{attackerLines.Screens.Count}, capital:{attackerLines.Capitals.Count}, carrier:{attackerLines.Carriers.Count}, sub:{attackerLines.Submarines.Count}) " +
            $"positioning {attackerPositioning:P0}, screenEff {attackerScreening.ScreeningEfficiency:P0}, carrierScreenEff {attackerScreening.CarrierScreeningEfficiency:P0}; " +
            $"defender(screen:{defenderLines.Screens.Count}, capital:{defenderLines.Capitals.Count}, carrier:{defenderLines.Carriers.Count}, sub:{defenderLines.Submarines.Count}) " +
            $"positioning {defenderPositioning:P0}, screenEff {defenderScreening.ScreeningEfficiency:P0}, carrierScreenEff {defenderScreening.CarrierScreeningEfficiency:P0}");
        state.HourlyLog.Add(
            $"Hour {hour}: attacker damage {NavalSurfaceCombatSimulator.GetTotalDamage(surfacePhase.AttackerActions):F1}, defender damage {NavalSurfaceCombatSimulator.GetTotalDamage(surfacePhase.DefenderActions):F1}, " +
            $"attacker ships {surfacePhase.AttackerAliveCount} (retreated {surfacePhase.AttackerRetreatedCount}), defender ships {surfacePhase.DefenderAliveCount} (retreated {surfacePhase.DefenderRetreatedCount})");
    }

    private void AppendAirPhaseLogs(int hour, SimulationState state, AirPhaseSnapshot airPhase)
    {
        if (!airPhase.AttackerSortie.IsSortieHour && !airPhase.DefenderSortie.IsSortieHour)
        {
            return;
        }

        state.HourlyLog.Add(
            $"Hour {hour}: air sortie - " +
            $"attacker carrier {airPhase.AttackerSortie.CarrierSortiePlanes}/{airPhase.AttackerSortie.CarrierAssignedPlanes} (sortie x{airPhase.AttackerSortie.CarrierSortieEfficiencyMultiplier:F2}, night traffic x{airPhase.AttackerSortie.CarrierTrafficMultiplier:F2}), " +
            $"external {airPhase.AttackerSortie.ExternalPlanesJoining}/{airPhase.AttackerSortie.ExternalEligiblePlanes} (cap {airPhase.AttackerSortie.ExternalJoinCap:F1}); " +
            $"defender carrier {airPhase.DefenderSortie.CarrierSortiePlanes}/{airPhase.DefenderSortie.CarrierAssignedPlanes} (sortie x{airPhase.DefenderSortie.CarrierSortieEfficiencyMultiplier:F2}, night traffic x{airPhase.DefenderSortie.CarrierTrafficMultiplier:F2}), " +
            $"external {airPhase.DefenderSortie.ExternalPlanesJoining}/{airPhase.DefenderSortie.ExternalEligiblePlanes} (cap {airPhase.DefenderSortie.ExternalJoinCap:F1})");
        state.HourlyLog.Add($"Hour {hour}: air disruption - placeholder disabled (fighter interception not implemented yet)");
        state.HourlyLog.Add(
            $"Hour {hour}: air targets - attacker bomber wings {airPhase.AttackerSortie.BomberWings}, picks {_navalAirCombatSimulator.FormatAirTargetSelectionSummary(airPhase.AttackerSelections)}; " +
            $"defender bomber wings {airPhase.DefenderSortie.BomberWings}, picks {_navalAirCombatSimulator.FormatAirTargetSelectionSummary(airPhase.DefenderSelections)}");
        state.HourlyLog.Add(
            $"Hour {hour}: air AA preemptive - attacker bombers shot down {airPhase.AttackerSelections.BombersShotDown}, " +
            $"defender bombers shot down {airPhase.DefenderSelections.BombersShotDown}");
        state.HourlyLog.Add(
            $"Hour {hour}: air strike damage - " +
            $"attacker HP {airPhase.AttackerSelections.TotalDamageDealt:F1}, Org {airPhase.AttackerSelections.TotalOrganizationDamageDealt:F1}; " +
            $"defender HP {airPhase.DefenderSelections.TotalDamageDealt:F1}, Org {airPhase.DefenderSelections.TotalOrganizationDamageDealt:F1}");
    }

    private static void AppendSurfacePhaseLogs(int hour, SimulationState state, SurfacePhaseSnapshot surfacePhase)
    {
        state.HourlyLog.Add($"Hour {hour}: attacker actions - {NavalSurfaceCombatSimulator.BuildActionSummary(surfacePhase.AttackerActions)}");
        state.HourlyLog.Add($"Hour {hour}: defender actions - {NavalSurfaceCombatSimulator.BuildActionSummary(surfacePhase.DefenderActions)}");
        state.HourlyLog.Add($"Hour {hour}: attacker hit summary - {NavalSurfaceCombatSimulator.BuildHitSummary(surfacePhase.AttackerActions)}");
        state.HourlyLog.Add($"Hour {hour}: defender hit summary - {NavalSurfaceCombatSimulator.BuildHitSummary(surfacePhase.DefenderActions)}");
        state.HourlyLog.Add($"Hour {hour}: attacker skips - {NavalSurfaceCombatSimulator.BuildSkipSummary(surfacePhase.AttackerActions)}");
        state.HourlyLog.Add($"Hour {hour}: defender skips - {NavalSurfaceCombatSimulator.BuildSkipSummary(surfacePhase.DefenderActions)}");
    }

    private HourSimulationResult EvaluateHourTermination(
        BattleScenario scenario,
        int hour,
        SimulationState state,
        SurfacePhaseSnapshot surfacePhase)
    {
        var retreatTermination = HandleRetreatTermination(scenario, hour, state, surfacePhase);

        if (retreatTermination is not null)
        {
            return retreatTermination.Value;
        }

        var annihilationTermination = HandleAnnihilationTermination(surfacePhase);

        if (annihilationTermination is not null)
        {
            return annihilationTermination.Value;
        }

        return new HourSimulationResult(
            surfacePhase.AttackerAliveCount,
            surfacePhase.DefenderAliveCount,
            surfacePhase.AttackerRetreatedCount,
            surfacePhase.DefenderRetreatedCount,
            true);
    }

    private HourSimulationResult? HandleRetreatTermination(
        BattleScenario scenario,
        int hour,
        SimulationState state,
        SurfacePhaseSnapshot surfacePhase)
    {
        if (surfacePhase.AttackerAliveCount != surfacePhase.AttackerRetreatedCount &&
            surfacePhase.DefenderAliveCount != surfacePhase.DefenderRetreatedCount)
        {
            return null;
        }

        if (scenario.ContinueAfterRetreat)
        {
            state.Reengagements++;
            ResetRetreatedShipsForNewEngagement(scenario.Attacker.Fleet.Ships);
            ResetRetreatedShipsForNewEngagement(scenario.Defender.Fleet.Ships);
            state.HourlyLog.Add($"Hour {hour}: all remaining ships on one side retreated; immediately starting a new engagement with non-sunk ships");
            return new HourSimulationResult(
                surfacePhase.AttackerAliveCount,
                surfacePhase.DefenderAliveCount,
                surfacePhase.AttackerRetreatedCount,
                surfacePhase.DefenderRetreatedCount,
                true);
        }

        state.HourlyLog.Add($"Hour {hour}: Battle ended since one side has retreated");
        return new HourSimulationResult(
            surfacePhase.AttackerAliveCount,
            surfacePhase.DefenderAliveCount,
            surfacePhase.AttackerRetreatedCount,
            surfacePhase.DefenderRetreatedCount,
            false);
    }

    private static HourSimulationResult? HandleAnnihilationTermination(SurfacePhaseSnapshot surfacePhase)
    {
        if (surfacePhase.AttackerAliveCount > 0 && surfacePhase.DefenderAliveCount > 0)
        {
            return null;
        }

        return new HourSimulationResult(
            surfacePhase.AttackerAliveCount,
            surfacePhase.DefenderAliveCount,
            surfacePhase.AttackerRetreatedCount,
            surfacePhase.DefenderRetreatedCount,
            false);
    }


    public int GetAliveShipCount(List<Ship> ships)
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

    public int GetRetreatedShipCount(List<Ship> ships)
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

    public void ResetRetreatedShipsForNewEngagement(List<Ship> ships)
    {
        foreach (var ship in ships)
        {
            if (ship.IsSunk)
            {
                continue;
            }

            if (ship.CurrentStatus is ShipStatus.Retreated or ShipStatus.Retreating)
            {
                ship.CurrentStatus = ShipStatus.Active;
                ship.RetreatProgress = 0;
            }
        }
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
