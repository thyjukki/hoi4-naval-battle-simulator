using NavySimulator.Domain;

namespace NavySimulator.Domain.Battles;

internal static class NavalSurfaceCombatSimulator
{
    private readonly record struct DamageCalculationResult(double Damage, bool DidCriticalHit, double CriticalDamageMultiplier);

    public static List<ActionResult> ResolveActions(
        List<Ship> firingOrder,
        BattleLines defenderLines,
        ScreeningSummary attackerScreening,
        ScreeningSummary defenderScreening,
        double positioning,
        bool battleHasCarriers,
        bool battleHasCapitals,
        bool dontRetreat,
        int hour,
        Random random,
        out int retreatEvents)
    {
        var results = new List<ActionResult>();
        retreatEvents = 0;

        foreach (var ship in firingOrder)
        {
            if (ship.IsSunk || ship.CurrentStatus == ShipStatus.Retreated)
            {
                continue;
            }

            var stats = ship.GetFinalStats();

            if (ResolveRetreating(ship, hour, attackerScreening, dontRetreat))
            {
                retreatEvents++;
            }

            if (ship.CurrentStatus == ShipStatus.Retreated)
            {
                results.Add(ActionResult.Skip(ship, WeaponType.Light, hour, "retreated"));
                continue;
            }

            var lightAction = ResolveWeaponAction(
                ship,
                WeaponType.Light,
                stats.LightAttack,
                stats.LightPiercing,
                hour,
                battleHasCarriers,
                battleHasCapitals,
                positioning,
                attackerScreening,
                defenderLines,
                defenderScreening,
                random);
            ApplyActionDamage(lightAction);
            results.Add(lightAction);

            var heavyAction = ResolveWeaponAction(
                ship,
                WeaponType.Heavy,
                stats.HeavyAttack,
                stats.HeavyPiercing,
                hour,
                battleHasCarriers,
                battleHasCapitals,
                positioning,
                attackerScreening,
                defenderLines,
                defenderScreening,
                random);
            ApplyActionDamage(heavyAction);
            results.Add(heavyAction);

            var torpedoAction = ResolveWeaponAction(
                ship,
                WeaponType.Torpedo,
                stats.TorpedoAttack,
                1,
                hour,
                battleHasCarriers,
                battleHasCapitals,
                positioning,
                attackerScreening,
                defenderLines,
                defenderScreening,
                random);
            ApplyActionDamage(torpedoAction);
            results.Add(torpedoAction);
        }

        return results;
    }

    private static void ApplyActionDamage(ActionResult action)
    {
        if (!action.Fired || action.Target is null)
        {
            return;
        }

        var targetWasAlive = !action.Target.IsSunk;
        var appliedDamage = action.Target.ApplyDamage(action.Damage);
        action.AppliedHpDamage = appliedDamage.HpDamage;
        action.AppliedOrganizationDamage = appliedDamage.OrganizationDamage;
        action.DidKillingBlow = targetWasAlive && action.Target.IsSunk;
    }

    public static double GetTotalDamage(List<ActionResult> actions)
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

    public static string BuildActionSummary(List<ActionResult> actions)
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

    public static string BuildHitSummary(List<ActionResult> actions)
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

    public static string BuildSkipSummary(List<ActionResult> actions)
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

    private static bool ResolveRetreating(Ship ship, int hour, ScreeningSummary screeningSummary, bool dontRetreat)
    {
        if (dontRetreat)
        {
            return false;
        }

        if (ship.CurrentStatus == ShipStatus.Retreating)
        {
            var retreatSpeed = Hoi4Defines.BASE_ESCAPE_SPEED;
            retreatSpeed += GetRetreatSpeedFromScreening(ship, screeningSummary);
            retreatSpeed += ship.GetFinalStats().Speed * Hoi4Defines.SPEED_TO_ESCAPE_SPEED / 100;

            var timeOfDay = hour % 24;
            if (timeOfDay is > Hoi4Defines.NightStartHour or < Hoi4Defines.NightEndHour)
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

            return false;
        }

        if (ship.CurrentStatus == ShipStatus.Retreated || hour < Hoi4Defines.COMBAT_MIN_DURATION)
        {
            return false;
        }

        var remainingHpRatio = ship.CurrentHP / ship.GetFinalStats().Hp;

        if (!(remainingHpRatio < Hoi4Defines.CombatMinStrRetreatChance))
        {
            return false;
        }

        var retreatChance = Hoi4Defines.COMBAT_RETREAT_DECISION_CHANCE;
        if (!(Random.Shared.NextDouble() < retreatChance))
        {
            return false;
        }

        ship.CurrentStatus = ShipStatus.Retreating;
        ship.AttemptedRetreat = true;
        ship.RetreatProgress = 0;
        return true;
    }

    private static double GetRetreatSpeedFromScreening(Ship ship, ScreeningSummary screeningSummary)
    {
        var capitalScreeningRetreatSpeed =
            Hoi4Defines.CapitalScreeningBonusRetreatSpeed * screeningSummary.CarrierScreeningEfficiency;
        var screeningRetreatSpeed = Hoi4Defines.ScreeningBonusRetreatSpeed * screeningSummary.ScreeningEfficiency;
        return ship.Design.Hull.Role switch
        {
            ShipRole.Capital => screeningRetreatSpeed,
            ShipRole.Carrier => screeningRetreatSpeed + capitalScreeningRetreatSpeed,
            ShipRole.Convoy => screeningRetreatSpeed + capitalScreeningRetreatSpeed,
            _ => 0.0
        };
    }

    private static ActionResult ResolveWeaponAction(
        Ship shooter,
        WeaponType weapon,
        double attackValue,
        double piercingValue,
        int hour,
        bool battleHasCarriers,
        bool battleHasCapitals,
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

        if (!HasShipLineActivated(shooter.Design.Hull.Role, hour, battleHasCarriers, battleHasCapitals))
        {
            return ActionResult.Skip(shooter, weapon, hour, "line-not-active");
        }

        if (!IsWeaponReadyThisHour(weapon, hour))
        {
            return ActionResult.Skip(shooter, weapon, hour, "cooldown");
        }

        var targetGroups = GetValidTargetGroups(weapon, defenderLines, defenderScreening, random);

        if (targetGroups.Count == 0)
        {
            return ActionResult.Skip(shooter, weapon, hour, "no-valid-target");
        }

        var selectedTarget = shooter.CurrentStatus == ShipStatus.Retreating
            ? SelectTargetDeterministically(weapon, targetGroups)
            : SelectTargetWeightedRandom(weapon, targetGroups, random);

        if (selectedTarget is null)
        {
            return ActionResult.Skip(shooter, weapon, hour, "no-valid-target");
        }

        var defenderStats = selectedTarget.Target.GetFinalStats();
        var defenderVisibility = selectedTarget.Target.Design.Hull.Role == ShipRole.Submarine
            ? defenderStats.SubVisibility
            : defenderStats.SurfaceVisibility;

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
        var didCriticalHit = false;
        var criticalDamageMultiplier = 1.0;
        if (didHit)
        {
            var damageResult = CalculateDamage(selectedTarget.Target, weapon, attackValue, piercingValue, positioning, random);
            damage = damageResult.Damage;
            didCriticalHit = damageResult.DidCriticalHit;
            criticalDamageMultiplier = damageResult.CriticalDamageMultiplier;
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
            didHit,
            didCriticalHit,
            criticalDamageMultiplier);
    }

    private static bool HasShipLineActivated(ShipRole role, int hour, bool battleHasCarriers, bool battleHasCapitals)
    {
        var elapsedCombatHours = Math.Max(0, hour - 1);
        return elapsedCombatHours >= GetActivationDelayHours(role, battleHasCarriers, battleHasCapitals);
    }

    private static int GetActivationDelayHours(ShipRole role, bool battleHasCarriers, bool battleHasCapitals)
    {
        return role switch
        {
            ShipRole.Carrier => Hoi4Defines.CARRIER_ONLY_COMBAT_ACTIVATE_TIME,
            ShipRole.Capital => battleHasCarriers ? Hoi4Defines.CAPITAL_ONLY_COMBAT_ACTIVATE_TIME : 0,
            _ => battleHasCapitals ? Hoi4Defines.ALL_SHIPS_ACTIVATE_TIME : 0
        };
    }

    private static bool IsWeaponReadyThisHour(WeaponType weapon, int hour)
    {
        var cooldownHours = GetCooldownHours(weapon);

        if (cooldownHours <= 0)
        {
            return true;
        }

        if (hour < cooldownHours)
        {
            return false;
        }

        return hour % cooldownHours == 0;
    }

    private static DamageCalculationResult CalculateDamage(
        Ship target,
        WeaponType weapon,
        double attackValue,
        double piercingValue,
        double positioning,
        Random random)
    {
        var targetStats = target.GetFinalStats();
        var piercingDamageValue = 1.0;
        var piercingThresholdIndex = Hoi4Defines.NAVY_PIERCING_THRESHOLDS.Length - 1;

        if (weapon is not WeaponType.Torpedo)
        {
            var targetArmor = targetStats.Armor;
            var effectiveArmor = Math.Max(0.0001, targetArmor);
            var piercingRatio = piercingValue / effectiveArmor;

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

        var criticalChance = GetCriticalHitChance(
            weapon,
            targetStats.Reliability,
            targetStats.TorpedoEnemyCriticalChanceFactor,
            piercingThresholdIndex);
        var criticalDamageMultiplier = GetCriticalDamageMultiplier(weapon, targetStats.Reliability);
        double damageFromCriticalHitMultiplier;
        if (random.NextDouble() <= criticalChance)
        {
            if (random.NextDouble() <= Hoi4Defines.CHANCE_TO_DAMAGE_PART_ON_CRITICAL_HIT)
            {
                //TODO Critical hit to a part would reduce the target's stats for the rest of the combat
                damageFromCriticalHitMultiplier = 1.0;
            }
            else
            {
                damageFromCriticalHitMultiplier = criticalDamageMultiplier;
            }
        }
        else
            damageFromCriticalHitMultiplier = 1.0;

        var positioningMultiplier = 1.0 - Hoi4Defines.DAMAGE_PENALTY_ON_MINIMUM_POSITIONING * (1.0 - positioning);
        var torpedoReductionMultiplier = 1.0;

        if (weapon == WeaponType.Torpedo)
        {
            torpedoReductionMultiplier = Math.Max(0, 1.0 - targetStats.TorpedoDamageReductionFactor);
        }

        var damage = attackValue * piercingDamageValue * damageFromCriticalHitMultiplier * positioningMultiplier * torpedoReductionMultiplier;
        damage *= (1.0 - Hoi4Defines.COMBAT_DAMAGE_RANDOMNESS +
                   Hoi4Defines.COMBAT_DAMAGE_RANDOMNESS * random.NextDouble());
        var finalDamage = Math.Max(0, damage);
        return new DamageCalculationResult(finalDamage, damageFromCriticalHitMultiplier > 1.0, damageFromCriticalHitMultiplier);
    }

    private static double GetCriticalHitChance(
        WeaponType weapon,
        double targetReliability,
        double torpedoEnemyCriticalChanceFactor,
        int piercingThresholdIndex)
    {
        if (weapon == WeaponType.Torpedo)
        {
            var modifiedTorpedoCriticalChance =
                Hoi4Defines.COMBAT_TORPEDO_CRITICAL_CHANCE * (1.0 + torpedoEnemyCriticalChanceFactor);
            return Math.Clamp(modifiedTorpedoCriticalChance, 0, 1);
        }

        var reliabilityFactor = 1.0 - Math.Clamp(targetReliability, 0, 1);

        if (reliabilityFactor <= 0)
        {
            return 0;
        }

        var piercingCriticalModifier = Hoi4Defines.NAVY_PIERCING_THRESHOLD_CRITICAL_VALUES[piercingThresholdIndex];
        var criticalChance = Hoi4Defines.COMBAT_BASE_CRITICAL_CHANCE * piercingCriticalModifier * reliabilityFactor;
        return Math.Clamp(criticalChance, 0, 1);
    }

    private static double GetCriticalDamageMultiplier(WeaponType weapon, double targetReliability)
    {
        if (weapon == WeaponType.Torpedo)
        {
            return Hoi4Defines.COMBAT_TORPEDO_CRITICAL_DAMAGE_MULT;
        }

        var reliabilityFactor = 1.0 - Math.Clamp(targetReliability, 0, 1);
        return 1.0 + (Hoi4Defines.COMBAT_CRITICAL_DAMAGE_MULT - 1.0) * reliabilityFactor;
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
        return weapon == WeaponType.DepthCharge
            ? Hoi4Defines.COMBAT_BASE_HIT_CHANCE * Hoi4Defines.DEPTH_CHARGES_HIT_CHANCE_MULT
            : Hoi4Defines.COMBAT_BASE_HIT_CHANCE;
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
        if (timeOfDay is > Hoi4Defines.NightStartHour or < Hoi4Defines.NightEndHour)
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
                    modifier *= 1 + attackerScreening.ScreeningEfficiency;
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
            modifier *= 1 + 0.3 * attackerScreening.ScreeningEfficiency;
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

                if (best is null ||
                    weight > best.Weight ||
                    (Math.Abs(weight - best.Weight) < 0.0001 && string.CompareOrdinal(targetShip.ID, best.Target.ID) < 0))
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
}

