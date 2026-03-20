namespace NavySimulator.Domain.Battles;

public class BattleSimulator
{
    public BattleResult Simulate(BattleScenario scenario)
    {
        var hourlyLog = new List<string>();
        var allActions = new List<ActionResult>();
        var cooldowns = new Dictionary<(string ShipID, WeaponType Weapon), int>();
        var random = new Random(42);
        var attackerFiringOrder = scenario.Attacker.Fleet.Ships.OrderBy(ship => ship.ID, StringComparer.Ordinal).ToList();
        var defenderFiringOrder = scenario.Defender.Fleet.Ships.OrderBy(ship => ship.ID, StringComparer.Ordinal).ToList();

        for (var hour = 1; hour <= scenario.MaxHours; hour++)
        {
            var attackerLines = BuildBattleLinesFromFleet(scenario.Attacker.Fleet.Ships);
            var defenderLines = BuildBattleLinesFromFleet(scenario.Defender.Fleet.Ships);

            var attackerShipCount = GetLineShipCount(attackerLines);
            var defenderShipCount = GetLineShipCount(defenderLines);
            var attackerPositioning = CalculateFleetSizePositioning(attackerShipCount, defenderShipCount);
            var defenderPositioning = CalculateFleetSizePositioning(defenderShipCount, attackerShipCount);
            
            var attackerScreening = CalculateScreening(attackerLines, attackerPositioning);
            var defenderScreening = CalculateScreening(defenderLines, defenderPositioning);

            var attackerActions = ResolveActions(
                attackerFiringOrder,
                defenderLines,
                attackerScreening,
                defenderScreening,
                attackerPositioning,
                hour,
                cooldowns,
                random);
            var defenderActions = ResolveActions(
                defenderFiringOrder,
                attackerLines,
                defenderScreening,
                attackerScreening,
                defenderPositioning,
                hour,
                cooldowns,
                random);

            ApplyActionDamage(attackerActions);
            ApplyActionDamage(defenderActions);
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
                $"Hour {hour}: attacker damage {GetTotalDamage(attackerActions):F1}, defender damage {GetTotalDamage(defenderActions):F1}, " +
                $"attacker ships {attackerAliveCount} (retreated {attackerRetreatedCount}), defender ships {defenderAliveCount} (retreated {defenderRetreatedCount})");
            hourlyLog.Add($"Hour {hour}: attacker actions - {BuildActionSummary(attackerActions)}");
            hourlyLog.Add($"Hour {hour}: defender actions - {BuildActionSummary(defenderActions)}");
            hourlyLog.Add($"Hour {hour}: attacker hit summary - {BuildHitSummary(attackerActions)}");
            hourlyLog.Add($"Hour {hour}: defender hit summary - {BuildHitSummary(defenderActions)}");
            hourlyLog.Add($"Hour {hour}: attacker skips - {BuildSkipSummary(attackerActions)}");
            hourlyLog.Add($"Hour {hour}: defender skips - {BuildSkipSummary(defenderActions)}");

            if (attackerAliveCount == attackerRetreatedCount || defenderAliveCount == defenderRetreatedCount)
            {
                hourlyLog.Add($"Hour {hour}: Battle ended since one side has retreated");
                return BuildResult(scenario, hour, attackerAliveCount, defenderAliveCount, attackerRetreatedCount, defenderRetreatedCount, hourlyLog, allActions);
            }
            
            if (attackerAliveCount == 0 || defenderAliveCount == 0)
            {
                return BuildResult(scenario, hour, attackerAliveCount, defenderAliveCount, attackerRetreatedCount, defenderRetreatedCount, hourlyLog, allActions);
            }
        }

        var finalAttackerAlive = GetAliveShipCount(scenario.Attacker.Fleet.Ships);
        var finalDefenderAlive = GetAliveShipCount(scenario.Defender.Fleet.Ships);
        
        var finalAttackerRetreated = GetRetreatedShipCount(scenario.Attacker.Fleet.Ships);
        var finalDefenderRetreated = GetRetreatedShipCount(scenario.Defender.Fleet.Ships);

        return BuildResult(scenario, scenario.MaxHours, finalAttackerAlive, finalDefenderAlive, finalAttackerRetreated, finalDefenderRetreated, hourlyLog, allActions);
    }

    private static BattleLines BuildBattleLinesFromFleet(List<Ship> ships)
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

    private static ScreeningSummary CalculateScreening(BattleLines lines, double positioning)
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

    private static int GetLineShipCount(BattleLines lines)
    {
        return lines.Screens.Count +
               lines.Capitals.Count +
               lines.Carriers.Count +
               lines.Submarines.Count +
               lines.Convoys.Count;
    }

    private static double CalculateFleetSizePositioning(int ownShipCount, int opponentShipCount)
    {
        if (ownShipCount < Hoi4Defines.MIN_SHIPS_FOR_HIGHER_SHIP_RATIO_PENALTY ||
            ownShipCount <= opponentShipCount)
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

    private static List<ActionResult> ResolveActions(
        List<Ship> firingOrder,
        BattleLines defenderLines,
        ScreeningSummary attackerScreening,
        ScreeningSummary defenderScreening,
        double positioning,
        int hour,
        Dictionary<(string ShipID, WeaponType Weapon), int> cooldowns,
        Random random)
    {
        var results = new List<ActionResult>();

        foreach (var ship in firingOrder)
        {
            if (ship.IsSunk || ship.CurrentStatus == ShipStatus.Retreated)
            {
                continue;
            }

            var stats = ship.GetFinalStats();

            ResolveRetreating(ship, hour, attackerScreening);

            if (ship.CurrentStatus == ShipStatus.Retreated)
            {
                results.Add(ActionResult.Skip(ship, WeaponType.Light, hour, "retreated"));
                continue;
            }

            results.Add(ResolveWeaponAction(
                ship,
                WeaponType.Light,
                stats.LightAttack,
                stats.LightPiercing,
                hour,
                cooldowns,
                positioning,
                attackerScreening,
                defenderLines,
                defenderScreening,
                random));
            results.Add(ResolveWeaponAction(
                ship,
                WeaponType.Heavy,
                stats.HeavyAttack,
                stats.HeavyPiercing,
                hour,
                cooldowns,
                positioning,
                attackerScreening,
                defenderLines,
                defenderScreening,
                random));
            results.Add(ResolveWeaponAction(
                ship,
                WeaponType.Torpedo,
                stats.TorpedoAttack,
                1, // Torpedo dont pierce
                hour,
                cooldowns,
                positioning,
                attackerScreening,
                defenderLines,
                defenderScreening,
                random));
        }

        return results;
    }

    private static void ResolveRetreating(Ship ship, int hour, ScreeningSummary screeningSummary)
    {
        if (ship.CurrentStatus == ShipStatus.Retreating)
        {
            var retreatSpeed = Hoi4Defines.BASE_ESCAPE_SPEED;
            retreatSpeed += GetRetreatSpeedFromScreening(ship, screeningSummary);
            retreatSpeed += ship.GetFinalStats().Speed * Hoi4Defines.SPEED_TO_ESCAPE_SPEED / 100;

            var timeOfDay = hour % 24;
            if (timeOfDay is > 17 or < 5)
            {
                retreatSpeed += Hoi4Defines.NightRetreatSpeed;
            }

            var daysFought = hour / 24;
            retreatSpeed += Math.Min(daysFought * Hoi4Defines.ESCAPE_SPEED_PER_COMBAT_DAY, Hoi4Defines.MAX_ESCAPE_SPEED_FROM_COMBAT_DURATION);
            
            ship.RetreatProgress += retreatSpeed / 24;
            if (ship.RetreatProgress >= 1)
            {
                ship.CurrentStatus = ShipStatus.Retreated;
            }
            return;
        }

        if (ship.CurrentStatus == ShipStatus.Retreated || hour < Hoi4Defines.COMBAT_MIN_DURATION)
        {
            return;
        } 
        
        var remainingHpRatio = ship.CurrentHP / ship.GetFinalStats().Hp;

        if (!(remainingHpRatio < Hoi4Defines.CombatMinStrRetreatChance)) return;
        var retreatChance = Hoi4Defines.COMBAT_RETREAT_DECISION_CHANCE;
        if (!(Random.Shared.NextDouble() < retreatChance)) return;
        ship.CurrentStatus = ShipStatus.Retreating;
        ship.AttemptedRetreat = true;
        ship.RetreatProgress = 0;
    }

    private static double GetRetreatSpeedFromScreening(Ship ship, ScreeningSummary screeningSummary)
    {
        var capitalScreeningRetreatSpeed =
            Hoi4Defines.CapitalScreeningBonusRetreatSpeed * screeningSummary.CarrierScreeningEfficiency;
        var screeningRetreatSpeed = Hoi4Defines.ScreeningBonusRetreatSpeed * screeningSummary.ScreeningEfficiency;
        switch (ship.Design.Hull.Role)
        {
            case ShipRole.Capital:
                return screeningRetreatSpeed;
            case ShipRole.Carrier:
            case ShipRole.Convoy:
                return screeningRetreatSpeed +  capitalScreeningRetreatSpeed;
            case ShipRole.Screen:
            case ShipRole.Submarine:
            default:
                return 0.0;
        }
    }

    private static ActionResult ResolveWeaponAction(
        Ship shooter,
        WeaponType weapon,
        double attackValue,
        double piercingValue,
        int hour,
        Dictionary<(string ShipID, WeaponType Weapon), int> cooldowns,
        double positioning,
        ScreeningSummary attackerScreening,
        BattleLines defenderLines,
        ScreeningSummary defenderScreening,
        Random random)
    {
        if (attackValue <= 0)
        {
            return ActionResult.Skip(shooter, weapon, hour, "no-weapon");
        }

        var cooldownKey = (shooter.ID, weapon);
        
        if (hour < Hoi4Defines.COMBAT_INITIAL_DURATION)
        {
            return ActionResult.Skip(shooter, weapon, hour, "initial-combat");
        }

        if (cooldowns.TryGetValue(cooldownKey, out var nextAvailableHour) && hour < nextAvailableHour)
        {
            return ActionResult.Skip(shooter, weapon, hour, "cooldown");
        }

        var targetGroups = GetValidTargetGroups(weapon, defenderLines, defenderScreening, random);

        if (targetGroups.Count == 0)
        {
            return ActionResult.Skip(shooter, weapon, hour, "no-valid-target");
        }

        var selectedTarget = shooter.CurrentStatus == ShipStatus.Retreating ? 
            SelectTargetDeterministically(weapon, targetGroups) : 
            SelectTargetWeightedRandom(weapon, targetGroups, random);

        if (selectedTarget is null)
        {
            return ActionResult.Skip(shooter, weapon, hour, "no-valid-target");
        }

        var defenderStats = selectedTarget.Target.GetFinalStats();
        var defenderVisibility = selectedTarget.Target.Design.Hull.Role == ShipRole.Submarine
            ? defenderStats.SubVisibility
            : defenderStats.SurfaceVisibility;

        cooldowns[cooldownKey] = hour + GetCooldownHours(weapon);
        var finalHitChance = CalculateFinalHitChance(
            shooter,
            selectedTarget.Target,
            weapon,
            attackerScreening,
            defenderScreening,
            hour);
        var hitRoll = random.NextDouble();
        var didHit = hitRoll <= finalHitChance;

        var damage = 0.0;
        if (didHit)
        {
            damage = CalculateDamage(selectedTarget.Target, weapon, attackValue, piercingValue, positioning);
        }

        return ActionResult.Fire(
            shooter,
            weapon,
            selectedTarget,
            damage,
            piercingValue,
            defenderStats.Armor,
            defenderStats.Speed,
            defenderVisibility,
            hour,
            finalHitChance,
            hitRoll,
            didHit);
    }

    private static double CalculateDamage(Ship target, WeaponType weapon, double attackValue, double piercingValue, double positioning)
    {
        var targetStats = target.GetFinalStats();
        var piercingDamageValue = 1.0;

        if (weapon is not WeaponType.Torpedo)
        {
            var targetArmor = targetStats.Armor;
            var effectiveArmor = Math.Max(0.0001, targetArmor);
            var piercingRatio = piercingValue / effectiveArmor;

            var piercingThresholdIndex = Hoi4Defines.NAVY_PIERCING_THRESHOLDS.Length - 1;
            for (var i = 0; i < Hoi4Defines.NAVY_PIERCING_THRESHOLDS.Length; i++)
            {
                if (piercingRatio >= Hoi4Defines.NAVY_PIERCING_THRESHOLDS[i])
                {
                    piercingThresholdIndex = i;
                    break;
                }
            }

            piercingDamageValue = Hoi4Defines.NAVY_PIERCING_THRESHOLD_DAMAGE_VALUES[piercingThresholdIndex];
        }
        
        var positioningMultiplier = 1.0 - Hoi4Defines.DAMAGE_PENALTY_ON_MINIMUM_POSITIONING * (1.0 - positioning);
        var torpedoReductionMultiplier = 1.0;

        if (weapon == WeaponType.Torpedo)
        {
            torpedoReductionMultiplier = Math.Max(0, 1.0 - targetStats.TorpedoDamageReductionFactor);
        }

        var damage = attackValue * piercingDamageValue * positioningMultiplier * torpedoReductionMultiplier;
        // random factor in damage. So if max damage is fe. 10 and randomness is 30% then damage will be between 7-10.
        damage *= (1.0 - Hoi4Defines.COMBAT_DAMAGE_RANDOMNESS +
                   Hoi4Defines.COMBAT_DAMAGE_RANDOMNESS * Random.Shared.NextDouble());
        damage = Math.Max(0, damage);
        return damage;
    }

    private static double CalculateFinalHitChance(
        Ship shooter,
        Ship target,
        WeaponType weapon,
        ScreeningSummary attackerScreening,
        ScreeningSummary defenderScreening,
        int hour)
    {
        var baseHitChance = GetBaseHitChance(weapon);
        var modifierPipeline = GetHitChanceModifier(shooter, weapon, attackerScreening, hour);
        var shipHitProfile = CalculateShipHitProfile(shooter, target, defenderScreening);
        var weaponHitProfile = GetWeaponHitProfile(weapon);
        var profileModifier = Math.Min(Math.Pow(shipHitProfile / weaponHitProfile, 2), 1.0);

        return Math.Max(baseHitChance * modifierPipeline * profileModifier, Hoi4Defines.COMBAT_MIN_HIT_CHANCE);
    }

    private static double GetBaseHitChance(WeaponType weapon)
    {
        if (weapon == WeaponType.DepthCharge)
        {
            return Hoi4Defines.COMBAT_BASE_HIT_CHANCE * Hoi4Defines.DEPTH_CHARGES_HIT_CHANCE_MULT;
        }

        return Hoi4Defines.COMBAT_BASE_HIT_CHANCE;
    }

    private static double GetWeaponHitProfile(WeaponType weapon)
    {
        return weapon switch
        {
            WeaponType.Light => Hoi4Defines.GUN_HIT_PROFILES_LIGHT,
            WeaponType.Heavy => Hoi4Defines.GUN_HIT_PROFILES_HEAVY,
            WeaponType.Torpedo => Hoi4Defines.GUN_HIT_PROFILES_TORPEDO,
            _ => Hoi4Defines.GUN_HIT_PROFILES_LIGHT
        };
    }

    private static double CalculateShipHitProfile(Ship shooter, Ship target, ScreeningSummary defenderScreening)
    {
        var stats = target.GetFinalStats();
        var visibility = target.Design.Hull.Role == ShipRole.Submarine ? stats.SubVisibility : stats.SurfaceVisibility;

        if (shooter.Design.Hull.Role is ShipRole.Capital or ShipRole.Carrier)
        {
            visibility *= 1 + (Hoi4Defines.ScreeningVisibiliityBonus * defenderScreening.ScreeningEfficiency);
        }
        
        
        var denominator = Hoi4Defines.HIT_PROFILE_SPEED_FACTOR * stats.Speed + Hoi4Defines.HIT_PROFILE_SPEED_BASE;

        return denominator <= 0
            ? Hoi4Defines.HIT_PROFILE_MULT
            : Hoi4Defines.HIT_PROFILE_MULT * visibility / denominator;
    }

    private static double GetHitChanceModifier(
        Ship shooter,
        WeaponType weapon,
        ScreeningSummary attackerScreening,
        int hour)
    {
        var modifier = 1.0;
        var timeOfDay = hour % 24;
        if (timeOfDay is > 17 or < 5)
        {
            modifier *= 1.0 + Hoi4Defines.NightHitChance;
        }

        var stats = shooter.GetFinalStats();

        switch (weapon)
        {
            case WeaponType.Light:
                modifier *= 1.0 + stats.LightHitChanceFactor;

                break;
            case WeaponType.Heavy:
                modifier *= 1.0 + stats.HeavyHitChanceFactor;
                if (shooter.Design.Hull.Role is ShipRole.Capital or ShipRole.Carrier)
                {
                    modifier *= 1 + 1 * attackerScreening.ScreeningEfficiency; // 100 heavy hit chance factor
                }
                break;
            case WeaponType.Torpedo:
                modifier *= 1.0 + stats.TorpedoHitChanceFactor;
                break;
            case WeaponType.DepthCharge:
                modifier *= 1.0 + stats.DepthChargeHitChanceFactor;
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(weapon), weapon, null);
        }

        var orgagnisatonPercentage = shooter.CurrentOrganization / stats.Organization;
        
        modifier *= 1.0 + Hoi4Defines.COMBAT_LOW_ORG_HIT_CHANCE_PENALTY * (1.0 - orgagnisatonPercentage);

        if (shooter.Design.Hull.Role is ShipRole.Capital or ShipRole.Carrier)
        {
            modifier *= 1 + 0.3 * attackerScreening.ScreeningEfficiency; // 30 hit chance factor
        }

        return modifier;
    }

    private static List<TargetGroup> GetValidTargetGroups(
        WeaponType weapon,
        BattleLines defenderLines,
        ScreeningSummary defenderScreening,
        Random random)
    {
        switch (weapon)
        {
            case WeaponType.Light:
                return GetClosestNonEmptyGroup(defenderLines, includeSubmarines: false);
            case WeaponType.Heavy:
                return GetFirstTwoNonEmptyGroups(defenderLines, includeSubmarines: false);
            case WeaponType.Torpedo:
            {
                var groups = new List<TargetGroup>();
                var bypassScreenChance = Math.Clamp(1.0 - defenderScreening.ScreeningEfficiency, 0, 1);
                var bypassCarrierScreenChance = Math.Clamp(1.0 - defenderScreening.CarrierScreeningEfficiency, 0, 1);

                // Torpedoes roll each shot for whether they bypass screening layers.
                var canBypassScreens = random.NextDouble() < bypassScreenChance;
                var canReachCarrierLine = canBypassScreens && random.NextDouble() < bypassCarrierScreenChance;

                if (defenderLines.Screens.Count > 0)
                {
                    groups.Add(new TargetGroup(GroupType.Screen, defenderLines.Screens));
                }

                if (canBypassScreens && defenderLines.Capitals.Count > 0)
                {
                    groups.Add(new TargetGroup(GroupType.Capital, defenderLines.Capitals));
                }

                if (canReachCarrierLine && defenderLines.Carriers.Count > 0)
                {
                    groups.Add(new TargetGroup(GroupType.Carrier, defenderLines.Carriers));
                }

                if (canReachCarrierLine && defenderLines.Convoys.Count > 0)
                {
                    groups.Add(new TargetGroup(GroupType.Convoy, defenderLines.Convoys));
                }

                if (groups.Count == 0)
                {
                    groups.AddRange(GetClosestNonEmptyGroup(defenderLines, includeSubmarines: false));
                }

                return groups;
            }
            default:
                return [];
        }
    }

    private static List<TargetGroup> GetClosestNonEmptyGroup(BattleLines lines, bool includeSubmarines)
    {
        var ordered = BuildOrderedGroups(lines, includeSubmarines);
        var closest = ordered.FirstOrDefault(group => group.Ships.Count > 0);

        return closest is null ? [] : [closest];
    }

    private static List<TargetGroup> GetFirstTwoNonEmptyGroups(BattleLines lines, bool includeSubmarines)
    {
        return BuildOrderedGroups(lines, includeSubmarines)
            .Where(group => group.Ships.Count > 0)
            .Take(2)
            .ToList();
    }

    private static List<TargetGroup> BuildOrderedGroups(BattleLines lines, bool includeSubmarines)
    {
        var groups = new List<TargetGroup>
        {
            new(GroupType.Screen, lines.Screens),
            new(GroupType.Capital, lines.Capitals),
            new(GroupType.Carrier, lines.Carriers),
            new(GroupType.Convoy, lines.Convoys)
        };

        if (includeSubmarines)
        {
            groups.Add(new TargetGroup(GroupType.Submarine, lines.Submarines));
        }

        return groups;
    }

    private static SelectedTarget? SelectTargetDeterministically(
        WeaponType weapon,
        List<TargetGroup> groups)
    {
        SelectedTarget? best = null;

        foreach (var group in groups)
        {
            foreach (var targetShip in group.Ships)
            {
                if (targetShip.IsSunk)
                {
                    continue;
                }

                var weight = GetTargetWeight(weapon, targetShip, group.GroupType);
                

                if (best is null || weight > best.Weight || (Math.Abs(weight - best.Weight) < 0.0001 && string.CompareOrdinal(targetShip.ID, best.Target.ID) < 0))
                {
                    best = new SelectedTarget(targetShip, group.GroupType, weight);
                }
            }
        }

        return best;
    }

    private static SelectedTarget? SelectTargetWeightedRandom(
        WeaponType weapon,
        List<TargetGroup> groups,
        Random random)
    {
        var totalWeight = 0.0;

        foreach (var group in groups)
        {
            foreach (var targetShip in group.Ships)
            {
                if (targetShip.IsSunk)
                {
                    continue;
                }

                var weight = GetTargetWeight(weapon, targetShip, group.GroupType);

                // If the target is escaping, the weight is reduced by 50%
                if (targetShip.CurrentStatus == ShipStatus.Retreating)
                {
                    weight *= Hoi4Defines.RetreatingTargetWeightMult;
                }

                if (weight > 0)
                {
                    totalWeight += weight;
                }
            }
        }

        if (totalWeight <= 0)
        {
            return null;
        }

        var randomValue = random.NextDouble() * totalWeight;
        var cumulativeWeight = 0.0;

        foreach (var group in groups)
        {
            foreach (var targetShip in group.Ships)
            {
                if (targetShip.IsSunk)
                {
                    continue;
                }

                var weight = GetTargetWeight(weapon, targetShip, group.GroupType);
                if (targetShip.CurrentStatus == ShipStatus.Retreating)
                {
                    weight *= Hoi4Defines.RetreatingTargetWeightMult;
                }

                if (weight <= 0)
                {
                    continue;
                }

                cumulativeWeight += weight;

                if (randomValue <= cumulativeWeight)
                {
                    return new SelectedTarget(targetShip, group.GroupType, weight);
                }
            }
        }

        return null;
    }

    private static double GetTargetWeight(WeaponType weapon, Ship target, GroupType groupType)
    {
        var baseWeight = groupType switch
        {
            GroupType.Capital => weapon == WeaponType.Light
                ? Hoi4Defines.TargetWeightCapitalLight
                : Hoi4Defines.TargetWeightCapitalHeavyTorpedo,
            GroupType.Screen => weapon == WeaponType.Light
                ? Hoi4Defines.TargetWeightScreenLight
                : Hoi4Defines.TargetWeightScreenHeavyTorpedo,
            GroupType.Carrier => weapon == WeaponType.Light
                ? Hoi4Defines.TargetWeightCarrierLight
                : Hoi4Defines.TargetWeightCarrierHeavyTorpedo,
            GroupType.Convoy => weapon == WeaponType.Light
                ? Hoi4Defines.TargetWeightConvoyLight
                : Hoi4Defines.TargetWeightConvoyHeavyTorpedo,
            GroupType.Submarine => Hoi4Defines.TargetWeightSubmarine,
            _ => Hoi4Defines.TargetWeightDefault
        };

        var maxHp = Math.Max(1, target.GetFinalStats().Hp);
        var damageRatio = 1.0 - target.CurrentHP / maxHp;
        var damagedWeightBonus = 1.0 + Math.Clamp(damageRatio, 0, 1);

        var retreatWeightBonus = target.CurrentStatus == ShipStatus.Retreating ? Hoi4Defines.RetreatingTargetWeightMult : 1.0;

        return baseWeight * damagedWeightBonus * retreatWeightBonus;
    }

    private static int GetCooldownHours(WeaponType weapon)
    {
        return weapon switch
        {
            WeaponType.Light => Hoi4Defines.BASE_GUN_COOLDOWNS_LIGHT,
            WeaponType.Heavy => Hoi4Defines.BASE_GUN_COOLDOWNS_HEAVY,
            WeaponType.Torpedo => Hoi4Defines.BASE_GUN_COOLDOWNS_TORPEDO,
            _ => Hoi4Defines.BASE_GUN_COOLDOWNS
        };
    }

    private static void ApplyActionDamage(List<ActionResult> actions)
    {
        foreach (var action in actions)
        {
            if (!action.Fired)
            {
                continue;
            }

            var targetWasAlive = action.Target is not null && !action.Target.IsSunk;
            var appliedDamage = action.Target!.ApplyDamage(action.Damage);
            action.AppliedHpDamage = appliedDamage.HpDamage;
            action.AppliedOrganizationDamage = appliedDamage.OrganizationDamage;
            action.DidKillingBlow = targetWasAlive && action.Target.IsSunk;
        }
    }

    private static double GetTotalDamage(List<ActionResult> actions)
    {
        var totalDamage = 0.0;

        foreach (var action in actions)
        {
            if (action.Fired)
            {
                totalDamage += action.Damage;
            }
        }

        return totalDamage;
    }

    private static string BuildActionSummary(List<ActionResult> actions)
    {
        var fired = actions.Where(action => action.Fired).ToList();

        if (fired.Count == 0)
        {
            return "no attacks fired";
        }

        var parts = new List<string>();

        foreach (var weapon in new[] { WeaponType.Light, WeaponType.Heavy, WeaponType.Torpedo })
        {
            var weaponActions = fired.Where(action => action.Weapon == weapon).ToList();

            if (weaponActions.Count == 0)
            {
                continue;
            }

            var targetGroups = string.Join(", ", weaponActions
                .GroupBy(action => action.TargetGroup)
                .Select(group => $"{group.Key}:{group.Count()}"));

            var hitCount = weaponActions.Count(action => action.DidHit);
            parts.Add($"{weapon}={hitCount}/{weaponActions.Count} [{targetGroups}]");
        }

        return string.Join("; ", parts);
    }

    private static string BuildHitSummary(List<ActionResult> actions)
    {
        var fired = actions.Where(action => action.Fired).ToList();

        if (fired.Count == 0)
        {
            return "no shots";
        }

        var averageChance = fired.Average(action => action.FinalHitChance);
        var hits = fired.Count(action => action.DidHit);

        return $"hits {hits}/{fired.Count}, avg chance {averageChance:P1}";
    }

    private static string BuildSkipSummary(List<ActionResult> actions)
    {
        var skipped = actions.Where(action => !action.Fired).ToList();

        if (skipped.Count == 0)
        {
            return "none";
        }

        return string.Join(", ", skipped
            .GroupBy(action => action.SkipReason)
            .Select(group => $"{group.Key}:{group.Count()}"));
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

    private static BattleResult BuildResult(
        BattleScenario scenario,
        int hoursElapsed,
        int attackerShipsRemaining,
        int defenderShipsRemaining,
        int attackerShipsRetreated,
        int defenderShipsRetreated,
        List<string> hourlyLog,
        List<ActionResult> allActions)
    {
        var attackerProductionLost = GetSunkProductionCost(scenario.Attacker.Fleet.Ships);
        var defenderProductionLost = GetSunkProductionCost(scenario.Defender.Fleet.Ships);
        var attackerToDefenderProductionLossRatio = SafeRatio(attackerProductionLost, defenderProductionLost);
        var defenderToAttackerProductionLossRatio = SafeRatio(defenderProductionLost, attackerProductionLost);
        var shipReports = BuildShipReports(scenario, allActions);

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
            hourlyLog,
            shipReports);
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

    private static List<ShipBattleReport> BuildShipReports(BattleScenario scenario, List<ActionResult> allActions)
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
                .Where(action => action.ShooterID == entry.Ship.ID && action.Fired && action.Target is not null && action.Damage > 0)
                .Select(action => new ShipDamageReportEntry(
                    action.Hour,
                    action.Target!.ID,
                    action.Weapon,
                    action.Damage,
                    action.AppliedHpDamage,
                    action.AppliedOrganizationDamage,
                    action.DidKillingBlow,
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
                damagedShips));
        }

        return reports;
    }
}

