namespace NavySimulator.Domain;

public class BattleSimulator
{
    private const double FullScreenRatioForCapitals = 3.0;
    private const double FullScreenRatioForConvoys = 0.5;
    private const double FullCarrierScreenRatioForCarriers = 1.0;
    private const double FullCarrierScreenRatioForConvoys = 0.25;
    private const int LightCooldownHours = 1;
    private const int HeavyCooldownHours = 1;
    private const int TorpedoCooldownHours = 4;
    private const double BaseGunHitChance = 0.10;
    private const double BaseTorpedoHitChance = 0.10;
    private const double MinHitChance = 0.05;
    private const double LightWeaponHitProfile = 45.0;
    private const double HeavyWeaponHitProfile = 80.0;
    private const double TorpedoWeaponHitProfile = 100.0;

    public BattleResult Simulate(BattleScenario scenario)
    {
        var hourlyLog = new List<string>();
        var cooldowns = new Dictionary<(string ShipID, WeaponType Weapon), int>();
        var random = new Random(42);

        for (var hour = 1; hour <= scenario.MaxHours; hour++)
        {
            var attackerAlive = GetAliveShips(scenario.Attacker.Fleet.Ships);
            var defenderAlive = GetAliveShips(scenario.Defender.Fleet.Ships);

            var attackerLines = BuildBattleLines(attackerAlive);
            var defenderLines = BuildBattleLines(defenderAlive);

            var attackerScreening = CalculateScreening(attackerLines, positioning: 1.0);
            var defenderScreening = CalculateScreening(defenderLines, positioning: 1.0);

            var attackerActions = ResolveActions(
                attackerLines,
                defenderLines,
                attackerScreening,
                defenderScreening,
                hour,
                cooldowns,
                random);
            var defenderActions = ResolveActions(
                defenderLines,
                attackerLines,
                defenderScreening,
                attackerScreening,
                hour,
                cooldowns,
                random);

            ApplyActionDamage(attackerActions);
            ApplyActionDamage(defenderActions);

            var attackerAliveCount = GetAliveShipCount(scenario.Attacker.Fleet.Ships);
            var defenderAliveCount = GetAliveShipCount(scenario.Defender.Fleet.Ships);

            hourlyLog.Add(
                $"Hour {hour}: " +
                $"attacker(screen:{attackerLines.Screens.Count}, capital:{attackerLines.Capitals.Count}, carrier:{attackerLines.Carriers.Count}, sub:{attackerLines.Submarines.Count}) " +
                $"screenEff {attackerScreening.ScreeningEfficiency:P0}, carrierScreenEff {attackerScreening.CarrierScreeningEfficiency:P0}; " +
                $"defender(screen:{defenderLines.Screens.Count}, capital:{defenderLines.Capitals.Count}, carrier:{defenderLines.Carriers.Count}, sub:{defenderLines.Submarines.Count}) " +
                $"screenEff {defenderScreening.ScreeningEfficiency:P0}, carrierScreenEff {defenderScreening.CarrierScreeningEfficiency:P0}");
            hourlyLog.Add(
                $"Hour {hour}: attacker damage {GetTotalDamage(attackerActions):F1}, defender damage {GetTotalDamage(defenderActions):F1}, " +
                $"attacker ships {attackerAliveCount}, defender ships {defenderAliveCount}");
            hourlyLog.Add($"Hour {hour}: attacker actions - {BuildActionSummary(attackerActions)}");
            hourlyLog.Add($"Hour {hour}: defender actions - {BuildActionSummary(defenderActions)}");
            hourlyLog.Add($"Hour {hour}: attacker hit summary - {BuildHitSummary(attackerActions)}");
            hourlyLog.Add($"Hour {hour}: defender hit summary - {BuildHitSummary(defenderActions)}");
            hourlyLog.Add($"Hour {hour}: attacker skips - {BuildSkipSummary(attackerActions)}");
            hourlyLog.Add($"Hour {hour}: defender skips - {BuildSkipSummary(defenderActions)}");

            if (attackerAliveCount == 0 || defenderAliveCount == 0)
            {
                return BuildResult(scenario, hour, attackerAliveCount, defenderAliveCount, hourlyLog);
            }
        }

        var finalAttackerAlive = GetAliveShipCount(scenario.Attacker.Fleet.Ships);
        var finalDefenderAlive = GetAliveShipCount(scenario.Defender.Fleet.Ships);

        return BuildResult(scenario, scenario.MaxHours, finalAttackerAlive, finalDefenderAlive, hourlyLog);
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
            FullScreenRatioForCapitals * (lines.Capitals.Count + lines.Carriers.Count) +
            FullScreenRatioForConvoys * lines.Convoys.Count;
        var effectiveScreens = lines.Screens.Count * contributionFactor;
        var screeningRatio = requiredScreens <= 0 ? 1.0 : effectiveScreens / requiredScreens;

        var requiredCapitals =
            FullCarrierScreenRatioForCarriers * lines.Carriers.Count +
            FullCarrierScreenRatioForConvoys * lines.Convoys.Count;
        var effectiveCapitals = lines.Capitals.Count * contributionFactor;
        var carrierScreeningRatio = requiredCapitals <= 0 ? 1.0 : effectiveCapitals / requiredCapitals;

        return new ScreeningSummary(
            Math.Clamp(screeningRatio, 0, 1),
            Math.Clamp(carrierScreeningRatio, 0, 1));
    }

    private static double GetPositioningContributionFactor(double positioning)
    {
        // At 0% positioning ships contribute 50%; at 100% they contribute fully.
        return 0.5 + 0.5 * Math.Clamp(positioning, 0, 1);
    }

    private static double GetTotalLightAttack(List<Ship> ships)
    {
        return ships.Sum(ship => ship.Design.GetFinalStats().LightAttack);
    }

    private static List<ActionResult> ResolveActions(
        BattleLines attackerLines,
        BattleLines defenderLines,
        ScreeningSummary attackerScreening,
        ScreeningSummary defenderScreening,
        int hour,
        Dictionary<(string ShipID, WeaponType Weapon), int> cooldowns,
        Random random)
    {
        var results = new List<ActionResult>();
        var firingShips = attackerLines.AllAliveShips.OrderBy(ship => ship.ID).ToList();

        foreach (var ship in firingShips)
        {
            var stats = ship.Design.GetFinalStats();

            results.Add(ResolveWeaponAction(
                ship,
                WeaponType.Light,
                stats.LightAttack,
                hour,
                cooldowns,
                attackerScreening,
                defenderLines,
                defenderScreening,
                random));
            results.Add(ResolveWeaponAction(
                ship,
                WeaponType.Heavy,
                stats.HeavyAttack,
                hour,
                cooldowns,
                attackerScreening,
                defenderLines,
                defenderScreening,
                random));
            results.Add(ResolveWeaponAction(
                ship,
                WeaponType.Torpedo,
                stats.TorpedoAttack,
                hour,
                cooldowns,
                attackerScreening,
                defenderLines,
                defenderScreening,
                random));
        }

        return results;
    }

    private static ActionResult ResolveWeaponAction(
        Ship shooter,
        WeaponType weapon,
        double attackValue,
        int hour,
        Dictionary<(string ShipID, WeaponType Weapon), int> cooldowns,
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

        if (cooldowns.TryGetValue(cooldownKey, out var nextAvailableHour) && hour < nextAvailableHour)
        {
            return ActionResult.Skip(shooter, weapon, "cooldown");
        }

        var targetGroups = GetValidTargetGroups(weapon, defenderLines, defenderScreening);

        if (targetGroups.Count == 0)
        {
            return ActionResult.Skip(shooter, weapon, "no-valid-target");
        }

        var selectedTarget = SelectTargetDeterministically(weapon, targetGroups);

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
        var damage = didHit ? attackValue : 0;

        return ActionResult.Fire(shooter, weapon, selectedTarget, damage, finalHitChance, hitRoll, didHit);
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

        return Math.Max(baseHitChance * modifierPipeline * profileModifier, MinHitChance);
    }

    private static double GetBaseHitChance(WeaponType weapon)
    {
        return weapon switch
        {
            WeaponType.Light => BaseGunHitChance,
            WeaponType.Heavy => BaseGunHitChance,
            WeaponType.Torpedo => BaseTorpedoHitChance,
            _ => BaseGunHitChance
        };
    }

    private static double GetWeaponHitProfile(WeaponType weapon)
    {
        return weapon switch
        {
            WeaponType.Light => LightWeaponHitProfile,
            WeaponType.Heavy => HeavyWeaponHitProfile,
            WeaponType.Torpedo => TorpedoWeaponHitProfile,
            _ => LightWeaponHitProfile
        };
    }

    private static double CalculateShipHitProfile(Ship target)
    {
        var stats = target.Design.GetFinalStats();
        var visibility = target.Design.Hull.Role == ShipRole.Submarine ? stats.SubVisibility : stats.SurfaceVisibility;
        var denominator = 0.5 * stats.Speed + 20;

        return denominator <= 0 ? 100 : 100 * visibility / denominator;
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
                groups.Add(new TargetGroup("Screen", defenderLines.Screens));
            }

            if (canBypassScreens && defenderLines.Capitals.Count > 0)
            {
                groups.Add(new TargetGroup("Capital", defenderLines.Capitals));
            }

            if (canReachCarrierLine && defenderLines.Carriers.Count > 0)
            {
                groups.Add(new TargetGroup("Carrier", defenderLines.Carriers));
            }

            if (canReachCarrierLine && defenderLines.Convoys.Count > 0)
            {
                groups.Add(new TargetGroup("Convoy", defenderLines.Convoys));
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
            new TargetGroup("Screen", lines.Screens),
            new TargetGroup("Capital", lines.Capitals),
            new TargetGroup("Carrier", lines.Carriers),
            new TargetGroup("Convoy", lines.Convoys)
        };

        if (includeSubmarines)
        {
            groups.Add(new TargetGroup("Submarine", lines.Submarines));
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
            foreach (var ship in group.Ships.Where(ship => !ship.IsSunk))
            {
                var weight = GetTargetWeight(weapon, ship, group.Name);

                if (best is null || weight > best.Weight || (Math.Abs(weight - best.Weight) < 0.0001 && string.CompareOrdinal(ship.ID, best.Target.ID) < 0))
                {
                    best = new SelectedTarget(ship, group.Name, weight);
                }
            }
        }

        return best;
    }

    private static double GetTargetWeight(WeaponType weapon, Ship target, string groupName)
    {
        var baseWeight = groupName switch
        {
            "Capital" => weapon == WeaponType.Light ? 2 : 30,
            "Screen" => weapon == WeaponType.Light ? 6 : 3,
            "Carrier" => weapon == WeaponType.Light ? 1 : 15,
            "Convoy" => weapon == WeaponType.Light ? 4 : 60,
            "Submarine" => 4,
            _ => 1
        };

        var maxHp = Math.Max(1, target.Design.GetFinalStats().HP);
        var damageRatio = 1.0 - target.CurrentHP / maxHp;
        var damagedWeightBonus = 1.0 + Math.Clamp(damageRatio, 0, 1);

        return baseWeight * damagedWeightBonus;
    }

    private static int GetCooldownHours(WeaponType weapon)
    {
        return weapon switch
        {
            WeaponType.Light => LightCooldownHours,
            WeaponType.Heavy => HeavyCooldownHours,
            WeaponType.Torpedo => TorpedoCooldownHours,
            _ => 1
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

    private static BattleResult BuildResult(
        BattleScenario scenario,
        int hoursElapsed,
        int attackerShipsRemaining,
        int defenderShipsRemaining,
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
            hourlyLog);
    }

    private sealed class BattleLines
    {
        public List<Ship> Screens;
        public List<Ship> Capitals;
        public List<Ship> Carriers;
        public List<Ship> Submarines;
        public List<Ship> Convoys;

        public BattleLines(
            List<Ship> screens,
            List<Ship> capitals,
            List<Ship> carriers,
            List<Ship> submarines,
            List<Ship> convoys)
        {
            Screens = screens;
            Capitals = capitals;
            Carriers = carriers;
            Submarines = submarines;
            Convoys = convoys;
        }

        public List<Ship> AllAliveShips =>
        [
            .. Screens,
            .. Capitals,
            .. Carriers,
            .. Submarines,
            .. Convoys
        ];
    }

    private sealed class ScreeningSummary
    {
        public double ScreeningEfficiency;
        public double CarrierScreeningEfficiency;

        public ScreeningSummary(double screeningEfficiency, double carrierScreeningEfficiency)
        {
            ScreeningEfficiency = screeningEfficiency;
            CarrierScreeningEfficiency = carrierScreeningEfficiency;
        }
    }

    private sealed class TargetGroup
    {
        public string Name;
        public List<Ship> Ships;

        public TargetGroup(string name, List<Ship> ships)
        {
            Name = name;
            Ships = ships;
        }
    }

    private sealed class SelectedTarget
    {
        public Ship Target;
        public string Group;
        public double Weight;

        public SelectedTarget(Ship target, string group, double weight)
        {
            Target = target;
            Group = group;
            Weight = weight;
        }
    }

    private sealed class ActionResult
    {
        public string ShooterID;
        public WeaponType Weapon;
        public bool Fired;
        public Ship? Target;
        public string TargetGroup;
        public double Damage;
        public double FinalHitChance;
        public double HitRoll;
        public bool DidHit;
        public string SkipReason;

        private ActionResult(
            string shooterId,
            WeaponType weapon,
            bool fired,
            Ship? target,
            string targetGroup,
            double damage,
            double finalHitChance,
            double hitRoll,
            bool didHit,
            string skipReason)
        {
            ShooterID = shooterId;
            Weapon = weapon;
            Fired = fired;
            Target = target;
            TargetGroup = targetGroup;
            Damage = damage;
            FinalHitChance = finalHitChance;
            HitRoll = hitRoll;
            DidHit = didHit;
            SkipReason = skipReason;
        }

        public static ActionResult Fire(
            Ship shooter,
            WeaponType weapon,
            SelectedTarget target,
            double damage,
            double finalHitChance,
            double hitRoll,
            bool didHit)
        {
            return new ActionResult(
                shooter.ID,
                weapon,
                true,
                target.Target,
                target.Group,
                damage,
                finalHitChance,
                hitRoll,
                didHit,
                string.Empty);
        }

        public static ActionResult Skip(Ship shooter, WeaponType weapon, string reason)
        {
            return new ActionResult(shooter.ID, weapon, false, null, string.Empty, 0, 0, 0, false, reason);
        }
    }

    private enum WeaponType
    {
        Light,
        Heavy,
        Torpedo
    }
}

