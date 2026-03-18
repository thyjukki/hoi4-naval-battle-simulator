namespace NavySimulator.Domain.Battles;

public class BattleSimulator
{
    public BattleResult Simulate(BattleScenario scenario)
    {
        var hourlyLog = new List<string>();
        var cooldowns = new Dictionary<(string ShipID, WeaponType Weapon), int>();
        var random = new Random(42);

        for (var hour = 1; hour <= scenario.MaxHours; hour++)
        {
            var attackerAlive = GetAliveShips(scenario.Attacker.Fleet.Ships);
            var defenderAlive = GetAliveShips(scenario.Defender.Fleet.Ships);
            attackerAlive = FilterRetreated(attackerAlive);
            defenderAlive = FilterRetreated(defenderAlive);

            var attackerLines = BuildBattleLines(attackerAlive);
            var defenderLines = BuildBattleLines(defenderAlive);

            var attackerPositioning = 1.0;
            var defenderPositioning = 1.0;
            
            var attackerScreening = CalculateScreening(attackerLines, attackerPositioning);
            var defenderScreening = CalculateScreening(defenderLines, defenderPositioning);

            var attackerActions = ResolveActions(
                attackerLines,
                defenderLines,
                attackerScreening,
                defenderScreening,
                attackerPositioning,
                hour,
                cooldowns,
                random);
            var defenderActions = ResolveActions(
                defenderLines,
                attackerLines,
                defenderScreening,
                attackerScreening,
                defenderPositioning,
                hour,
                cooldowns,
                random);

            ApplyActionDamage(attackerActions);
            ApplyActionDamage(defenderActions);

            var attackerAliveCount = GetAliveShipCount(scenario.Attacker.Fleet.Ships);
            var defenderAliveCount = GetAliveShipCount(scenario.Defender.Fleet.Ships);
            
            var attackerRetreatedCount = GetRetreatedShipCount(scenario.Attacker.Fleet.Ships);
            var defenderRetreatedCount = GetRetreatedShipCount(scenario.Defender.Fleet.Ships);

            hourlyLog.Add(
                $"Hour {hour}: " +
                $"attacker(screen:{attackerLines.Screens.Count}, capital:{attackerLines.Capitals.Count}, carrier:{attackerLines.Carriers.Count}, sub:{attackerLines.Submarines.Count}) " +
                $"screenEff {attackerScreening.ScreeningEfficiency:P0}, carrierScreenEff {attackerScreening.CarrierScreeningEfficiency:P0}; " +
                $"defender(screen:{defenderLines.Screens.Count}, capital:{defenderLines.Capitals.Count}, carrier:{defenderLines.Carriers.Count}, sub:{defenderLines.Submarines.Count}) " +
                $"screenEff {defenderScreening.ScreeningEfficiency:P0}, carrierScreenEff {defenderScreening.CarrierScreeningEfficiency:P0}");
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
                return BuildResult(scenario, hour, attackerAliveCount, defenderAliveCount, attackerRetreatedCount, defenderRetreatedCount, hourlyLog);
            }
            
            if (attackerAliveCount == 0 || defenderAliveCount == 0)
            {
                return BuildResult(scenario, hour, attackerAliveCount, defenderAliveCount, attackerRetreatedCount, defenderRetreatedCount, hourlyLog);
            }
        }

        var finalAttackerAlive = GetAliveShipCount(scenario.Attacker.Fleet.Ships);
        var finalDefenderAlive = GetAliveShipCount(scenario.Defender.Fleet.Ships);
        
        var finalAttackerRetreated = GetRetreatedShipCount(scenario.Attacker.Fleet.Ships);
        var finalDefenderRetreated = GetRetreatedShipCount(scenario.Defender.Fleet.Ships);

        return BuildResult(scenario, scenario.MaxHours, finalAttackerAlive, finalDefenderAlive, finalAttackerRetreated, finalDefenderRetreated, hourlyLog);
    }

    private static List<Ship> FilterRetreated(List<Ship> ships)
    {
        return ships.Where(ship => ship.CurrentStatus != ShipStatus.Retreated).ToList();
    }

    private static List<Ship> GetAliveShips(List<Ship> ships)
    {
        return ships.Where(ship => !ship.IsSunk).ToList();
    }

    private static BattleLines BuildBattleLines(List<Ship> ships)
    {
        return new BattleLines(
            ships.Where(ship => ship.Design.Hull.Role == ShipRole.Screen).ToList(),
            ships.Where(ship => ship.Design.Hull.Role == ShipRole.Capital).ToList(),
            ships.Where(ship => ship.Design.Hull.Role == ShipRole.Carrier).ToList(),
            ships.Where(ship => ship.Design.Hull.Role == ShipRole.Submarine).ToList(),
            ships.Where(ship => ship.Design.Hull.Role == ShipRole.Convoy).ToList());
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

    private static double GetPositioningContributionFactor(double positioning)
    {
        // At 0% positioning ships contribute 50%; at 100% they contribute fully.
        return Hoi4Defines.PositioningBaseContribution +
               Hoi4Defines.PositioningContributionScale * Math.Clamp(positioning, 0, 1);
    }

    private static List<ActionResult> ResolveActions(
        BattleLines attackerLines,
        BattleLines defenderLines,
        ScreeningSummary attackerScreening,
        ScreeningSummary defenderScreening,
        double positioning,
        int hour,
        Dictionary<(string ShipID, WeaponType Weapon), int> cooldowns,
        Random random)
    {
        var results = new List<ActionResult>();
        var firingShips = attackerLines.AllAliveShips.OrderBy(ship => ship.ID).ToList();

        foreach (var ship in firingShips)
        {
            var stats = ship.Design.GetFinalStats();

            ResolveRetreating(ship, hour, attackerScreening);

            if (ship.CurrentStatus == ShipStatus.Retreated)
            {
                results.Add(ActionResult.Skip(ship, WeaponType.Light, "retreated"));
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
            retreatSpeed += ship.Design.GetFinalStats().Speed * Hoi4Defines.SPEED_TO_ESCAPE_SPEED / 100;

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
        
        var remainingHpRatio = ship.CurrentHP / ship.Design.GetFinalStats().HP;

        if (!(remainingHpRatio < Hoi4Defines.CombatMinStrRetreatChance)) return;
        var retreatChance = Hoi4Defines.COMBAT_RETREAT_DECISION_CHANCE;
        if (!(Random.Shared.NextDouble() < retreatChance)) return;
        ship.CurrentStatus = ShipStatus.Retreating;
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
            return ActionResult.Skip(shooter, weapon, "no-weapon");
        }

        var cooldownKey = (shooter.ID, weapon);
        
        if (hour < Hoi4Defines.COMBAT_INITIAL_DURATION)
        {
            return ActionResult.Skip(shooter, weapon, "initial-combat");
        }

        if (cooldowns.TryGetValue(cooldownKey, out var nextAvailableHour) && hour < nextAvailableHour)
        {
            return ActionResult.Skip(shooter, weapon, "cooldown");
        }

        var targetGroups = GetValidTargetGroups(weapon, defenderLines, defenderScreening);

        if (targetGroups.Count == 0)
        {
            return ActionResult.Skip(shooter, weapon, "no-valid-target");
        }

        var selectedTarget = shooter.CurrentStatus == ShipStatus.Retreating ? 
            SelectTargetDeterministically(weapon, targetGroups) : 
            SelectTargetWeightedRandom(weapon, targetGroups);

        if (selectedTarget is null)
        {
            return ActionResult.Skip(shooter, weapon, "no-valid-target");
        }

        cooldowns[cooldownKey] = hour + GetCooldownHours(weapon);
        var finalHitChance = CalculateFinalHitChance(
            shooter,
            selectedTarget.Target,
            weapon,
            attackerScreening,
            defenderScreening);
        var hitRoll = random.NextDouble();
        var didHit = hitRoll <= finalHitChance;

        var damage = 0.0;
        if (didHit)
        {
            damage = CalculateDamage(selectedTarget.Target, attackValue, piercingValue, positioning);
        }

        return ActionResult.Fire(shooter, weapon, selectedTarget, damage, finalHitChance, hitRoll, didHit);
    }

    private static double CalculateDamage(Ship target, double attackValue, double piercingValue, double positioning)
    {
        var targetArmor = target.Design.GetFinalStats().Armor;
        var piercingRatio = piercingValue / targetArmor;
        
        var piercingThresholdIndex = Hoi4Defines.NAVY_PIERCING_THRESHOLDS
            .Select((threshold, index) => (threshold, index))
            .FirstOrDefault(t => piercingRatio >= t.threshold).index;
        var piercingDamageValue = Hoi4Defines.NAVY_PIERCING_THRESHOLD_DAMAGE_VALUES[piercingThresholdIndex];
        
        var positioningMultiplier = 1.0 - Hoi4Defines.DAMAGE_PENALTY_ON_MINIMUM_POSITIONING * (1.0 - positioning);
        return attackValue * piercingDamageValue * positioningMultiplier;
    }

    private static double CalculateFinalHitChance(
        Ship shooter,
        Ship target,
        WeaponType weapon,
        ScreeningSummary attackerScreening,
        ScreeningSummary defenderScreening)
    {
        var baseHitChance = GetBaseHitChance(weapon);
        var weaponHitProfile = GetWeaponHitProfile(weapon);
        var shipHitProfile = CalculateShipHitProfile(target);
        var profileModifier = Math.Min(Math.Pow(shipHitProfile / weaponHitProfile, 2), 1.0);
        var modifierPipeline = GetHitChanceModifier(shooter, target, weapon, attackerScreening, defenderScreening);

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

    private static double CalculateShipHitProfile(Ship target)
    {
        var stats = target.Design.GetFinalStats();
        var visibility = target.Design.Hull.Role == ShipRole.Submarine ? stats.SubVisibility : stats.SurfaceVisibility;
        var denominator = Hoi4Defines.HIT_PROFILE_SPEED_FACTOR * stats.Speed + Hoi4Defines.HIT_PROFILE_SPEED_BASE;

        return denominator <= 0
            ? Hoi4Defines.HIT_PROFILE_MULT
            : Hoi4Defines.HIT_PROFILE_MULT * visibility / denominator;
    }

    private static double GetHitChanceModifier(
        Ship shooter,
        Ship target,
        WeaponType weapon,
        ScreeningSummary attackerScreening,
        ScreeningSummary defenderScreening)
    {
        _ = shooter;
        _ = target;
        _ = weapon;
        _ = attackerScreening;
        _ = defenderScreening;

        // Placeholder pipeline for night/weather/commander/doctrine/screening hit modifiers.
        return 1.0;
    }

    private static List<TargetGroup> GetValidTargetGroups(
        WeaponType weapon,
        BattleLines defenderLines,
        ScreeningSummary defenderScreening)
    {
        if (weapon == WeaponType.Light)
        {
            return GetClosestNonEmptyGroup(defenderLines, includeSubmarines: false);
        }

        if (weapon == WeaponType.Heavy)
        {
            return GetFirstTwoNonEmptyGroups(defenderLines, includeSubmarines: false);
        }

        if (weapon == WeaponType.Torpedo)
        {
            var groups = new List<TargetGroup>();
            var canBypassScreens = (1.0 - defenderScreening.ScreeningEfficiency) > 0;
            var canReachCarrierLine =
                canBypassScreens && (1.0 - defenderScreening.CarrierScreeningEfficiency) > 0;

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

        return [];
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
            foreach (var targetShip in group.Ships.Where(ship => !ship.IsSunk))
            {
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
        List<TargetGroup> groups)
    {        
        var weightedTargets = new List<SelectedTarget>();

        foreach (var group in groups)
        {
            foreach (var targetShip in group.Ships.Where(ship => !ship.IsSunk))
            {
                var weight = GetTargetWeight(weapon, targetShip, group.GroupType);

                // If the target is escaping, the weight is reduced by 50%
                if (targetShip.CurrentStatus == ShipStatus.Retreating)
                {
                    weight *= Hoi4Defines.RetreatingTargetWeightMult;
                }

                if (weight > 0)
                {
                    weightedTargets.Add(new SelectedTarget(targetShip, group.GroupType, weight));
                }
            }
        }

        var totalWeight = weightedTargets.Sum(t => t.Weight);
        var randomValue = new Random().NextDouble() * totalWeight;
        var cumulativeWeight = 0.0;

        foreach (var target in weightedTargets)
        {
            cumulativeWeight += target.Weight;

            if (randomValue <= cumulativeWeight)
            {
                return target;
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

        var maxHp = Math.Max(1, target.Design.GetFinalStats().HP);
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
        foreach (var action in actions.Where(action => action.Fired))
        {
            action.Target!.ApplyDamage(action.Damage);
        }
    }

    private static double GetTotalDamage(List<ActionResult> actions)
    {
        return actions.Where(action => action.Fired).Sum(action => action.Damage);
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
        return ships.Count(ship => !ship.IsSunk);
    }

    private static int GetRetreatedShipCount(List<Ship> ships)
    {
        return ships.Count(ship => ship.CurrentStatus == ShipStatus.Retreated);
    }

    private static BattleResult BuildResult(
        BattleScenario scenario,
        int hoursElapsed,
        int attackerShipsRemaining,
        int defenderShipsRemaining,
        int attackerShipsRetreated,
        int defenderShipsRetreated,
        List<string> hourlyLog)
    {
        var outcome = "Draw";

        if (attackerShipsRemaining > 0 && defenderShipsRemaining == 0)
        {
            outcome = "Attacker Victory";
        }
        else if (defenderShipsRemaining > 0 && attackerShipsRemaining == 0)
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
            hourlyLog);
    }
}

