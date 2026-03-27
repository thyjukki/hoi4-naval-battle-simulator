using NavySimulator.Domain;

namespace NavySimulator.Domain.Battles;

internal class NavalAirCombatSimulator
{
    public virtual AirSortieSnapshot CalculateAirSortieSnapshot(
        BattleScenario scenario,
        BattleParticipant participant,
        BattleLines ownLines,
        ScreeningSummary ownScreening,
        BattleLines enemyLines,
        IReadOnlyDictionary<string, CarrierWingState> carrierWingStatesByWingKey,
        int hour)
    {
        var sortieDelayHours = Math.Max(1, (int)Math.Round(Hoi4Defines.CARRIER_HOURS_DELAY_AFTER_EACH_COMBAT));
        var elapsedCombatHours = Math.Max(0, hour - 1);
        var isSortieHour = elapsedCombatHours % sortieDelayHours == 0;

        if (!isSortieHour)
        {
            return new AirSortieSnapshot(
                false,
                0,
                0,
                0,
                1.0,
                0,
                0,
                0,
                0,
                0,
                0,
                0,
                0,
                0,
                0,
                0,
                0,
                new Dictionary<string, int>(StringComparer.Ordinal),
                new Dictionary<string, int>(StringComparer.Ordinal),
                []);
        }

        var carrierAssignedPlanesByShipId = GetCarrierAssignedBomberPlanesByShip(ownLines, carrierWingStatesByWingKey);
        var carrierAssignedPlanesByPlane = GetCarrierAssignedBomberPlanesByPlane(ownLines, carrierWingStatesByWingKey);
        var carrierAssignedPlanes = carrierAssignedPlanesByPlane.Values.Sum();
        var carrierBaseSortieEfficiency = CalculateCarrierSortieEfficiency(participant, ownLines, ownScreening);
        var carrierSortieEfficiencyByShipId = ownLines.Carriers
            .ToDictionary(
                carrier => carrier.ID,
                carrier => carrierBaseSortieEfficiency * GetCarrierOrganizationSortieMultiplier(carrier),
                StringComparer.Ordinal);
        var carrierTrafficMultiplier = IsNightHour(hour)
            ? Math.Clamp(1.0 + Hoi4Defines.NightCarrierTraffic, 0, 1)
            : 1.0;
        var carrierTotalFlightMultiplierByShipId = carrierSortieEfficiencyByShipId
            .ToDictionary(
                entry => entry.Key,
                entry => Math.Clamp(entry.Value * carrierTrafficMultiplier, 0, 1),
                StringComparer.Ordinal);
        var carrierSortieEfficiency = CalculateWeightedCarrierSortieEfficiency(
            carrierSortieEfficiencyByShipId,
            carrierAssignedPlanesByShipId);
        var carrierStrikeWingProfiles = BuildCarrierStrikeWingProfiles(
            ownLines,
            carrierWingStatesByWingKey,
            carrierTotalFlightMultiplierByShipId);
        var carrierSortiePlanesByShipId = carrierStrikeWingProfiles
            .GroupBy(profile => profile.CarrierShipID)
            .ToDictionary(group => group.Key, group => group.Sum(profile => profile.SortiePlanes), StringComparer.Ordinal);
        var carrierBomberWingCount = carrierStrikeWingProfiles.Count;
        var carrierSortiePlanes = carrierStrikeWingProfiles.Sum(profile => profile.SortiePlanes);
        var carrierBomberWingsByPlaneType = carrierStrikeWingProfiles
            .GroupBy(profile => profile.PlaneTypeLabel)
            .ToDictionary(group => group.Key, group => group.Count(), StringComparer.Ordinal);

        // Temporary assumption: Naval Strike mission efficiency is fixed at 100% for external planes.
        const double externalMissionEfficiency = 1.0;
        var externalEligiblePlanes = (int)Math.Floor(participant.ExternalNavalStrikePlanes * externalMissionEfficiency);

        var enemyCurrentHp = SumCurrentHp(enemyLines);
        var combatDays = (hour - 1) / 24.0;
        var externalJoinCap = Math.Max(
            Hoi4Defines.NAVAL_COMBAT_EXTERNAL_PLANES_MIN_CAP,
            enemyCurrentHp * Hoi4Defines.NAVAL_COMBAT_EXTERNAL_PLANES_JOIN_RATIO *
            (1.0 + Hoi4Defines.NAVAL_COMBAT_EXTERNAL_PLANES_JOIN_RATIO_PER_DAY * combatDays));
        var externalPlanesJoining = Math.Min(externalEligiblePlanes, (int)Math.Floor(externalJoinCap));

        // Air combat disruption phase hook.
        // For now this intentionally does nothing until fighter interception is implemented.
        const int enemyFightersPresentInAirZone = 0;
        carrierSortiePlanes = ApplyAirCombatDisruptionPlaceholder(carrierSortiePlanes, enemyFightersPresentInAirZone);
        externalPlanesJoining = ApplyAirCombatDisruptionPlaceholder(externalPlanesJoining, enemyFightersPresentInAirZone);
        var bomberWings = carrierBomberWingCount + (externalPlanesJoining / 10);
        var (carrierAvgAgility, carrierAvgNavalAttack, carrierAvgNavalTargeting) = CalculateWeightedBomberStats(
            scenario,
            carrierStrikeWingProfiles);
        var externalFallbackAgility = carrierAvgAgility;
        var externalFallbackNavalAttack = carrierAvgNavalAttack;
        var externalFallbackNavalTargeting = carrierAvgNavalTargeting;

        return new AirSortieSnapshot(
            true,
            carrierAssignedPlanes,
            carrierSortiePlanes,
            carrierSortieEfficiency,
            carrierTrafficMultiplier,
            externalEligiblePlanes,
            externalPlanesJoining,
            externalJoinCap,
            bomberWings,
            carrierBomberWingCount,
            externalPlanesJoining / 10,
            carrierAvgAgility,
            carrierAvgNavalAttack,
            carrierAvgNavalTargeting,
            externalFallbackAgility,
            externalFallbackNavalAttack,
            externalFallbackNavalTargeting,
            carrierBomberWingsByPlaneType,
            carrierSortiePlanesByShipId,
            carrierStrikeWingProfiles);
    }

    public virtual Dictionary<string, CarrierWingState> BuildCarrierWingStatesByWingKey(Fleet fleet)
    {
        var wingStatesByWingKey = new Dictionary<string, CarrierWingState>(StringComparer.Ordinal);
        var carriers = fleet.Ships.Where(ship => ship.Design.Hull.Role == ShipRole.Carrier);

        foreach (var carrier in carriers)
        {
            if (!fleet.CarrierAirwingsByShipDesign.TryGetValue(carrier.Design.ID, out var assignments))
            {
                continue;
            }

            foreach (var assignment in assignments)
            {
                for (var wingNumber = 1; wingNumber <= assignment.Airwings; wingNumber++)
                {
                    var wingKey = $"{carrier.ID}:{assignment.Type}:{assignment.PlaneID}:{wingNumber}";
                    wingStatesByWingKey[wingKey] = new CarrierWingState(
                        wingKey,
                        carrier.ID,
                        assignment.PlaneID,
                        assignment.Type,
                        10);
                }
            }
        }

        return wingStatesByWingKey;
    }

    public virtual void ApplyCarrierWingLosses(
        IReadOnlyDictionary<string, CarrierWingState> carrierWingStatesByWingKey,
        IReadOnlyDictionary<string, int> lossesByWingKey)
    {
        foreach (var loss in lossesByWingKey)
        {
            if (!carrierWingStatesByWingKey.TryGetValue(loss.Key, out var wing))
            {
                continue;
            }

            wing.CurrentPlanes = Math.Max(0, wing.CurrentPlanes - Math.Max(0, loss.Value));
        }
    }

    public virtual NavalStrikeSelectionSummary ResolveNavalStrike(
        BattleLines enemyLines,
        AirSortieSnapshot snapshot,
        Random random)
    {
        var selections = new Dictionary<string, int>(StringComparer.Ordinal);
        var carrierSelections = new Dictionary<string, int>(StringComparer.Ordinal);
        var carrierBombersShotDownByShipId = new Dictionary<string, int>(StringComparer.Ordinal);
        var carrierBombersShotDownByWingKey = new Dictionary<string, int>(StringComparer.Ordinal);
        var bombersShotDown = 0;
        var carrierBombersShotDown = 0;
        var totalDamage = 0.0;
        var totalOrganizationDamage = 0.0;
        var carrierDamage = 0.0;
        var carrierOrganizationDamage = 0.0;
        var carrierTargetAaDefenseTotal = 0.0;
        var carrierFleetAaReductionTotal = 0.0;
        var carrierSelectionCount = 0;
        var damageByPlaneType = new Dictionary<string, double>(StringComparer.Ordinal);

        if (snapshot.BomberWings <= 0)
        {
            return new NavalStrikeSelectionSummary(
                targetSelections: selections,
                carrierTargetSelections: new Dictionary<string, int>(StringComparer.Ordinal),
                carrierBombersShotDownByShipId: carrierBombersShotDownByShipId,
                carrierBombersShotDownByWingKey: carrierBombersShotDownByWingKey,
                bombersShotDown: 0,
                carrierBombersShotDown: 0,
                totalDamageDealt: 0,
                totalOrganizationDamageDealt: 0,
                carrierDamageDealt: 0,
                carrierOrganizationDamageDealt: 0,
                carrierAverageTargetAaDefense: 0,
                carrierAverageCombinedFleetAaDamageReduction: 0,
                damageByPlaneType: damageByPlaneType);
        }

        var candidates = BuildAirTargetCandidates(enemyLines).ToList();

        if (candidates.Count == 0)
        {
            return new NavalStrikeSelectionSummary(
                targetSelections: selections,
                carrierTargetSelections: new Dictionary<string, int>(StringComparer.Ordinal),
                carrierBombersShotDownByShipId: carrierBombersShotDownByShipId,
                carrierBombersShotDownByWingKey: carrierBombersShotDownByWingKey,
                bombersShotDown: 0,
                carrierBombersShotDown: 0,
                totalDamageDealt: 0,
                totalOrganizationDamageDealt: 0,
                carrierDamageDealt: 0,
                carrierOrganizationDamageDealt: 0,
                carrierAverageTargetAaDefense: 0,
                carrierAverageCombinedFleetAaDamageReduction: 0,
                damageByPlaneType: damageByPlaneType);
        }

        foreach (var carrierWing in snapshot.CarrierStrikeWingProfiles)
        {
            const bool isCarrierBased = true;
            var planeTypeLabel = carrierWing.PlaneTypeLabel;
            var wingAgility = snapshot.CarrierBomberAverageAgility;
            var wingNavalAttack = snapshot.CarrierBomberAverageNavalAttack;
            var wingNavalTargeting = snapshot.CarrierBomberAverageNavalTargeting;

            var target = SelectWeightedAirTarget(candidates, random);

            if (target is null)
            {
                continue;
            }

            selections[target.ID] = selections.TryGetValue(target.ID, out var current) ? current + 1 : 1;
            carrierSelections[target.ID] = carrierSelections.TryGetValue(target.ID, out var carrierCurrent) ? carrierCurrent + 1 : 1;
            var shotDown = Math.Min(carrierWing.SortiePlanes, ResolvePreemptiveAntiAirDefense(target, wingAgility, random));
            bombersShotDown += shotDown;
            carrierBombersShotDown += shotDown;
            carrierBombersShotDownByShipId[carrierWing.CarrierShipID] = carrierBombersShotDownByShipId.TryGetValue(carrierWing.CarrierShipID, out var currentLosses)
                ? currentLosses + shotDown
                : shotDown;
            carrierBombersShotDownByWingKey[carrierWing.WingKey] = shotDown;

            var planesRemaining = Math.Max(0, carrierWing.SortiePlanes - shotDown);
            var strikeBreakdown = CalculateNavalStrikeDamageBreakdown(
                isCarrierBased,
                wingNavalAttack,
                wingNavalTargeting,
                planesRemaining,
                target,
                enemyLines);
            carrierTargetAaDefenseTotal += strikeBreakdown.TargetedAaDefense;
            carrierFleetAaReductionTotal += strikeBreakdown.CombinedFleetAaDamageReduction;
            carrierSelectionCount++;

            if (planesRemaining <= 0)
            {
                continue;
            }

            var strikeDamage = strikeBreakdown.FinalDamageBeforeHpClamp;

            if (strikeDamage <= 0)
            {
                continue;
            }

            var applied = target.ApplyDamage(strikeDamage);
            // TODO: Apply torpedo-like critical hit handling for naval air strike hits.
            totalDamage += applied.HpDamage;
            totalOrganizationDamage += applied.OrganizationDamage;
            carrierDamage += applied.HpDamage;
            carrierOrganizationDamage += applied.OrganizationDamage;
            damageByPlaneType[planeTypeLabel] = damageByPlaneType.TryGetValue(planeTypeLabel, out var planeTypeDamage)
                ? planeTypeDamage + applied.HpDamage
                : applied.HpDamage;
        }

        for (var externalWingIndex = 0; externalWingIndex < snapshot.ExternalBomberWings; externalWingIndex++)
        {
            const bool isCarrierBased = false;
            var wingAgility = snapshot.ExternalBomberAverageAgility;
            var wingNavalAttack = snapshot.ExternalBomberAverageNavalAttack;
            var wingNavalTargeting = snapshot.ExternalBomberAverageNavalTargeting;

            var target = SelectWeightedAirTarget(candidates, random);

            if (target is null)
            {
                continue;
            }

            selections[target.ID] = selections.TryGetValue(target.ID, out var current) ? current + 1 : 1;
            var shotDown = ResolvePreemptiveAntiAirDefense(target, wingAgility, random);
            bombersShotDown += shotDown;

            var planesRemaining = Math.Max(0, 10 - shotDown);

            if (planesRemaining <= 0)
            {
                continue;
            }

            var strikeBreakdown = CalculateNavalStrikeDamageBreakdown(
                isCarrierBased,
                wingNavalAttack,
                wingNavalTargeting,
                planesRemaining,
                target,
                enemyLines);
            var strikeDamage = strikeBreakdown.FinalDamageBeforeHpClamp;

            if (strikeDamage <= 0)
            {
                continue;
            }

            var applied = target.ApplyDamage(strikeDamage);
            // TODO: Apply torpedo-like critical hit handling for naval air strike hits.
            totalDamage += applied.HpDamage;
            totalOrganizationDamage += applied.OrganizationDamage;
        }

        var averageCarrierTargetAaDefense = carrierSelectionCount <= 0 ? 0 : carrierTargetAaDefenseTotal / carrierSelectionCount;
        var averageCarrierFleetAaReduction = carrierSelectionCount <= 0 ? 0 : carrierFleetAaReductionTotal / carrierSelectionCount;

        return new NavalStrikeSelectionSummary(
            selections,
            carrierSelections,
            carrierBombersShotDownByShipId,
            carrierBombersShotDownByWingKey,
            bombersShotDown,
            carrierBombersShotDown,
            totalDamage,
            totalOrganizationDamage,
            carrierDamage,
            carrierOrganizationDamage,
            averageCarrierTargetAaDefense,
            averageCarrierFleetAaReduction,
            damageByPlaneType);
    }

    public virtual string FormatAirTargetSelectionSummary(NavalStrikeSelectionSummary summary)
    {
        var selections = summary.TargetSelections;

        if (selections.Count == 0)
        {
            return "none";
        }

        return string.Join(", ", selections
            .OrderByDescending(entry => entry.Value)
            .ThenBy(entry => entry.Key, StringComparer.Ordinal)
            .Take(5)
            .Select(entry => $"{entry.Key}:{entry.Value}"));
    }

    public virtual void AccumulateCarrierSorties(
        AirSortieSnapshot snapshot,
        NavalStrikeSelectionSummary strikeSummary,
        int hour,
        Dictionary<string, int> aggregateSortiesByShipId,
        Dictionary<string, Dictionary<int, CarrierSortieHourMetrics>> aggregateSortiesByShipIdAndHour)
    {
        if (!snapshot.IsSortieHour || snapshot.CarrierSortiePlanes <= 0 || snapshot.CarrierSortiePlanesByShipId.Count == 0)
        {
            return;
        }

        foreach (var entry in snapshot.CarrierSortiePlanesByShipId)
        {
            aggregateSortiesByShipId[entry.Key] = aggregateSortiesByShipId.TryGetValue(entry.Key, out var current)
                ? current + entry.Value
                : entry.Value;

            if (!aggregateSortiesByShipIdAndHour.TryGetValue(entry.Key, out var sortiesByHour))
            {
                sortiesByHour = new Dictionary<int, CarrierSortieHourMetrics>();
                aggregateSortiesByShipIdAndHour[entry.Key] = sortiesByHour;
            }

            var share = snapshot.CarrierSortiePlanes <= 0 ? 0 : entry.Value / (double)snapshot.CarrierSortiePlanes;
            var selectedTargets = FormatCarrierTargetSelectionSummary(strikeSummary.CarrierTargetSelections);
            var planesLost = strikeSummary.CarrierBombersShotDownByShipId.TryGetValue(entry.Key, out var lostForShip) ? lostForShip : 0;

            if (sortiesByHour.TryGetValue(hour, out var currentForHour))
            {
                currentForHour.SortiePlanes += entry.Value;
                currentForHour.PlanesLost += planesLost;
                currentForHour.FinalDamageDealt += strikeSummary.CarrierDamageDealt * share;
            }
            else
            {
                sortiesByHour[hour] = new CarrierSortieHourMetrics(
                    entry.Value,
                    planesLost,
                    selectedTargets,
                    strikeSummary.CarrierAverageTargetAaDefense,
                    strikeSummary.CarrierAverageCombinedFleetAaDamageReduction,
                    strikeSummary.CarrierDamageDealt * share);
            }
        }
    }

    private static List<StrikeWingProfile> BuildCarrierStrikeWingProfiles(
        BattleLines ownLines,
        IReadOnlyDictionary<string, CarrierWingState> carrierWingStatesByWingKey,
        IReadOnlyDictionary<string, double> carrierTotalFlightMultiplierByShipId)
    {
        var activeCarrierIdsInOrder = ownLines.Carriers.Select(ship => ship.ID).ToList();
        var activeCarrierIds = activeCarrierIdsInOrder.ToHashSet(StringComparer.Ordinal);
        var carrierOrderById = activeCarrierIdsInOrder
            .Select((carrierId, index) => new { carrierId, index })
            .ToDictionary(entry => entry.carrierId, entry => entry.index, StringComparer.Ordinal);
        var profiles = new List<StrikeWingProfile>();

        var activeBomberWings = carrierWingStatesByWingKey.Values
            .Where(wing => wing.Type == AirwingType.Bomber && wing.CurrentPlanes > 0 && activeCarrierIds.Contains(wing.CarrierShipID))
            .OrderBy(wing => carrierOrderById[wing.CarrierShipID])
            .ThenBy(wing => wing.WingKey, StringComparer.Ordinal)
            .ToList();

        foreach (var carrierId in activeCarrierIdsInOrder)
        {
            if (!carrierTotalFlightMultiplierByShipId.TryGetValue(carrierId, out var carrierTotalFlightMultiplier) ||
                carrierTotalFlightMultiplier <= 0)
            {
                continue;
            }

            var carrierWings = activeBomberWings
                .Where(wing => wing.CarrierShipID == carrierId)
                .OrderBy(wing => wing.WingKey, StringComparer.Ordinal)
                .ToList();

            var carrierTotalAvailablePlanes = carrierWings.Sum(wing => wing.CurrentPlanes);
            var sortiePlaneBudget = (int)Math.Floor(carrierTotalAvailablePlanes * carrierTotalFlightMultiplier);

            if (sortiePlaneBudget <= 0)
            {
                continue;
            }

            foreach (var wing in carrierWings)
            {
                var sortiePlanes = Math.Min(wing.CurrentPlanes, sortiePlaneBudget);

                if (sortiePlanes <= 0)
                {
                    continue;
                }

                profiles.Add(new StrikeWingProfile(
                    wing.WingKey,
                    wing.CarrierShipID,
                    wing.PlaneID,
                    sortiePlanes,
                    isCarrierBased: true));

                sortiePlaneBudget -= sortiePlanes;

                if (sortiePlaneBudget <= 0)
                {
                    break;
                }
            }
        }

        return profiles;
    }

    private static double CalculateWeightedCarrierSortieEfficiency(
        IReadOnlyDictionary<string, double> carrierSortieEfficiencyByShipId,
        IReadOnlyDictionary<string, int> carrierAssignedPlanesByShipId)
    {
        var totalAssignedPlanes = 0;
        var weightedSortieEfficiency = 0.0;

        foreach (var entry in carrierAssignedPlanesByShipId)
        {
            totalAssignedPlanes += entry.Value;

            if (!carrierSortieEfficiencyByShipId.TryGetValue(entry.Key, out var carrierSortieEfficiency))
            {
                continue;
            }

            weightedSortieEfficiency += carrierSortieEfficiency * entry.Value;
        }

        if (totalAssignedPlanes <= 0)
        {
            return 0;
        }

        return Math.Clamp(weightedSortieEfficiency / totalAssignedPlanes, 0, 1);
    }

    private static double GetCarrierOrganizationSortieMultiplier(Ship carrier)
    {
        var maxOrganization = carrier.GetFinalStats().Organization;

        if (maxOrganization <= 0)
        {
            return 0;
        }

        return Math.Clamp(carrier.CurrentOrganization / maxOrganization, 0, 1);
    }

    private static double CalculateCarrierSortieEfficiency(
        BattleParticipant participant,
        BattleLines ownLines,
        ScreeningSummary ownScreening)
    {
        if (ownLines.Carriers.Count == 0)
        {
            return 0;
        }

        var spiritSortieBonus = participant.Spirits
            .Where(spirit => spirit.AppliesTo(ShipRole.Carrier, ["Carrier"]))
            .Sum(spirit => spirit.SortieEffiency);

        // HOI4 quirk: any non-zero screen presence grants the full screening-based sortie bonus.
        var screeningSortieBonusMultiplier = ownLines.Screens.Count > 0 ? 1.0 : 0.0;

        var sortieEfficiency = Hoi4Defines.BASE_CARRIER_SORTIE_EFFICIENCY +
                               Hoi4Defines.CARRIER_SORTIE_EFFICIENCY_FROM_SCREENING * screeningSortieBonusMultiplier +
                               Hoi4Defines.CARRIER_SORTIE_EFFICIENCY_FROM_CAPITAL_SCREENING * ownScreening.CarrierScreeningEfficiency +
                               spiritSortieBonus;

        return Math.Clamp(sortieEfficiency, 0, 1);
    }

    private static bool IsNightHour(int hour)
    {
        var timeOfDay = hour % 24;
        return timeOfDay is > Hoi4Defines.NightStartHour or < Hoi4Defines.NightEndHour;
    }

    private static int ApplyAirCombatDisruptionPlaceholder(int attackingPlanes, int enemyFightersPresent)
    {
        _ = enemyFightersPresent;
        return attackingPlanes;
    }

    private static Dictionary<string, int> GetCarrierAssignedBomberPlanesByPlane(
        BattleLines ownLines,
        IReadOnlyDictionary<string, CarrierWingState> carrierWingStatesByWingKey)
    {
        var activeCarrierIds = ownLines.Carriers.Select(ship => ship.ID).ToHashSet(StringComparer.Ordinal);
        var assignedPlanesByPlane = new Dictionary<string, int>(StringComparer.Ordinal);

        foreach (var wing in carrierWingStatesByWingKey.Values)
        {
            if (wing.Type != AirwingType.Bomber || wing.CurrentPlanes <= 0 || !activeCarrierIds.Contains(wing.CarrierShipID))
            {
                continue;
            }

            assignedPlanesByPlane[wing.PlaneID] = assignedPlanesByPlane.TryGetValue(wing.PlaneID, out var current)
                ? current + wing.CurrentPlanes
                : wing.CurrentPlanes;
        }

        return assignedPlanesByPlane;
    }

    private static Dictionary<string, int> GetCarrierAssignedBomberPlanesByShip(
        BattleLines ownLines,
        IReadOnlyDictionary<string, CarrierWingState> carrierWingStatesByWingKey)
    {
        var activeCarrierIds = ownLines.Carriers.Select(ship => ship.ID).ToHashSet(StringComparer.Ordinal);
        var assignedPlanesByShip = new Dictionary<string, int>(StringComparer.Ordinal);

        foreach (var wing in carrierWingStatesByWingKey.Values)
        {
            if (wing.Type != AirwingType.Bomber || wing.CurrentPlanes <= 0 || !activeCarrierIds.Contains(wing.CarrierShipID))
            {
                continue;
            }

            assignedPlanesByShip[wing.CarrierShipID] = assignedPlanesByShip.TryGetValue(wing.CarrierShipID, out var current)
                ? current + wing.CurrentPlanes
                : wing.CurrentPlanes;
        }

        return assignedPlanesByShip;
    }

    private static (double AvgAgility, double AvgNavalAttack, double AvgNavalTargeting) CalculateWeightedBomberStats(
        BattleScenario scenario,
        IReadOnlyCollection<StrikeWingProfile> strikeWingProfiles)
    {
        var weightedAgility = 0.0;
        var weightedNavalAttack = 0.0;
        var weightedNavalTargeting = 0.0;
        var weightedPlanes = 0.0;

        foreach (var wing in strikeWingProfiles)
        {
            if (wing.SortiePlanes <= 0)
            {
                continue;
            }

            if (!scenario.PlanesByID.TryGetValue(wing.PlaneTypeLabel, out var plane))
            {
                continue;
            }

            weightedAgility += wing.SortiePlanes * plane.Stats.Agility;
            weightedNavalAttack += wing.SortiePlanes * plane.Stats.NavalAttack;
            weightedNavalTargeting += wing.SortiePlanes * plane.Stats.NavalTargeting;
            weightedPlanes += wing.SortiePlanes;
        }

        if (weightedPlanes <= 0)
        {
            return (0.0, 0.0, 0.0);
        }

        return (
            weightedAgility / weightedPlanes,
            weightedNavalAttack / weightedPlanes,
            weightedNavalTargeting / weightedPlanes);
    }

    private static double SumCurrentHp(BattleLines lines)
    {
        return lines.Screens.Sum(ship => ship.CurrentHP) +
               lines.Capitals.Sum(ship => ship.CurrentHP) +
               lines.Carriers.Sum(ship => ship.CurrentHP) +
               lines.Submarines.Sum(ship => ship.CurrentHP) +
               lines.Convoys.Sum(ship => ship.CurrentHP);
    }

    private static NavalStrikeDamageBreakdown CalculateNavalStrikeDamageBreakdown(
        bool isCarrierBased,
        double navalAttack,
        double navalTargeting,
        int planesRemaining,
        Ship target,
        BattleLines defendingLines)
    {
        var hitRatio = Math.Clamp((navalTargeting / 10.0) * Hoi4Defines.NAVAL_STRIKE_TARGETTING_TO_AMOUNT, 0, 1);
        var hitPlanes = planesRemaining * hitRatio;
        var rawDamage = hitPlanes * navalAttack;

        if (isCarrierBased)
        {
            rawDamage *= Hoi4Defines.NAVAL_STRIKE_CARRIER_MULTIPLIER;
        }

        var targetStats = target.GetFinalStats();
        var targetedAa = target.Design.Hull.Role == ShipRole.Convoy ? 0.0 : Math.Max(0, targetStats.AntiAir);
        var fleetAa = SumFleetAntiAir(defendingLines);
        var aaPool = targetedAa + Hoi4Defines.SHIP_TO_FLEET_ANTI_AIR_RATIO * fleetAa;
        var reduction = 1 - Math.Pow(Math.Max(0, aaPool), Hoi4Defines.ANTI_AIR_POW_ON_INCOMING_AIR_DAMAGE) * Hoi4Defines.ANTI_AIR_MULT_ON_INCOMING_AIR_DAMAGE;
        var damageMultiplier = Math.Clamp(reduction, 0, 1);

        return new NavalStrikeDamageBreakdown(
            Math.Max(0, rawDamage * damageMultiplier),
            targetedAa,
            1.0 - damageMultiplier);
    }

    private static double SumFleetAntiAir(BattleLines lines)
    {
        return lines.Screens.Sum(ship => ship.GetFinalStats().AntiAir) +
               lines.Capitals.Sum(ship => ship.GetFinalStats().AntiAir) +
               lines.Carriers.Sum(ship => ship.GetFinalStats().AntiAir) +
               lines.Submarines.Sum(ship => ship.GetFinalStats().AntiAir) +
               lines.Convoys.Sum(ship => ship.GetFinalStats().AntiAir);
    }

    private static int ResolvePreemptiveAntiAirDefense(Ship target, double bomberWingAverageAgility, Random random)
    {
        var aaChance = Hoi4Defines.ANTI_AIR_TARGETTING_TO_CHANCE *
                       Math.Max(Hoi4Defines.ANTI_AIR_TARGETING - Hoi4Defines.AIR_AGILITY_TO_NAVAL_STRIKE_AGILITY * bomberWingAverageAgility, 0.01);

        if (random.NextDouble() > aaChance)
        {
            return 0;
        }

        var aa = Math.Max(0, target.GetFinalStats().AntiAir);
        var destroyedPlanes = 10 * aa * Hoi4Defines.ANTI_AIR_ATTACK_TO_AMOUNT;
        return StochasticRound(destroyedPlanes, random);
    }

    private static int StochasticRound(double value, Random random)
    {
        if (value <= 0)
        {
            return 0;
        }

        var floor = (int)Math.Floor(value);
        var fraction = value - floor;
        var randomValue = random.NextDouble();
        return floor + (randomValue < fraction ? 1 : 0);
    }

    private static IEnumerable<(Ship Target, double Weight)> BuildAirTargetCandidates(BattleLines enemyLines)
    {
        var allShips = enemyLines.Screens
            .Concat(enemyLines.Capitals)
            .Concat(enemyLines.Carriers)
            .Concat(enemyLines.Convoys)
            .Concat(enemyLines.Submarines);

        foreach (var ship in allShips)
        {
            if (ship.Design.Hull.Role == ShipRole.Submarine)
            {
                // Hidden submarine state is not implemented yet; submarines are excluded for now.
                continue;
            }

            var weight = CalculateAirTargetWeight(ship);

            if (weight > 0)
            {
                yield return (ship, weight);
            }
        }
    }

    private static Ship? SelectWeightedAirTarget(List<(Ship Target, double Weight)> candidates, Random random)
    {
        var totalWeight = candidates.Sum(candidate => candidate.Weight);

        if (totalWeight <= 0)
        {
            return null;
        }

        var roll = random.NextDouble() * totalWeight;
        var cumulative = 0.0;

        foreach (var candidate in candidates)
        {
            cumulative += candidate.Weight;

            if (roll <= cumulative)
            {
                return candidate.Target;
            }
        }

        return candidates[^1].Target;
    }

    private static double CalculateAirTargetWeight(Ship ship)
    {
        var stats = ship.GetFinalStats();
        var maxHp = Math.Max(0, stats.Hp);

        if (maxHp <= 0)
        {
            return 0;
        }

        var weight = maxHp;
        weight *= GetAirTargetRoleScale(ship.Design.Hull.Role);

        var healthRatio = maxHp <= 0 ? 0 : Math.Clamp(ship.CurrentHP / maxHp, 0, 1);
        var missingHealthRatio = 1.0 - healthRatio;
        weight *= 1.0 + Hoi4Defines.NAVAL_COMBAT_AIR_STRENGTH_TARGET_SCORE * missingHealthRatio;

        if (stats.AntiAir < Hoi4Defines.NAVAL_COMBAT_AIR_LOW_AA_TARGET_SCORE)
        {
            var lowAaMultiplier = Hoi4Defines.NAVAL_COMBAT_AIR_LOW_AA_TARGET_SCORE *
                                  (Hoi4Defines.NAVAL_COMBAT_AIR_LOW_AA_TARGET_SCORE - stats.AntiAir);
            weight *= Math.Max(1.0, lowAaMultiplier);
        }

        return weight;
    }

    private static double GetAirTargetRoleScale(ShipRole role)
    {
        return role switch
        {
            ShipRole.Submarine => Hoi4Defines.NAVAL_COMBAT_AIR_SUB_TARGET_SCALE,
            ShipRole.Screen => Hoi4Defines.NAVAL_COMBAT_AIR_SCREEN_TARGET_SCALE,
            ShipRole.Capital => Hoi4Defines.NAVAL_COMBAT_AIR_CAPITAL_TARGET_SCALE,
            ShipRole.Carrier => Hoi4Defines.NAVAL_COMBAT_AIR_CARRIER_TARGET_SCALE,
            _ => 1.0
        };
    }

    private static string FormatCarrierTargetSelectionSummary(IReadOnlyDictionary<string, int> selections)
    {
        if (selections.Count == 0)
        {
            return "none";
        }

        return string.Join(", ", selections
            .OrderByDescending(entry => entry.Value)
            .ThenBy(entry => entry.Key, StringComparer.Ordinal)
            .Take(3)
            .Select(entry => $"{entry.Key}:{entry.Value}"));
    }
}

