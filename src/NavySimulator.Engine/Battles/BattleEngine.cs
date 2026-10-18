using NavySimulator.Engine.Battles.Mechanics;
using NavySimulator.Engine.Defines;
using NavySimulator.Engine.Random;
using NavySimulator.Engine.Results;

namespace NavySimulator.Engine.Battles;

/// <summary>
/// Runs a battle hour by hour. Per hour (S-HOUR in docs/mechanics/surface.md):
/// 1. rebuild battle lines, positioning and screening for both sides;
/// 2. air phase (not implemented yet, milestone 2);
/// 3. surface phase: every ship fires each ready weapon, in the order set by <see cref="BattleRules.FiringOrder"/>;
/// 4. retreat phase: retreating ships move away, damaged ships may decide to retreat;
/// 5. experience gain, optional daily manpower recovery, end-of-battle check.
/// </summary>
public sealed class BattleEngine
{
    private static readonly WeaponType[] WeaponOrder = [WeaponType.Light, WeaponType.Heavy, WeaponType.Torpedo, WeaponType.DepthCharge];

    public BattleResult Simulate(BattleScenario scenario, int seed)
    {
        return Simulate(scenario, new SeededRandomSource(seed), seed);
    }

    public BattleResult Simulate(BattleScenario scenario, IRandomSource random, int seed = 0)
    {
        var run = new Run(scenario, random);
        return run.Execute(seed);
    }

    private sealed class Run
    {
        private readonly BattleScenario scenario;
        private readonly GameDefines defines;
        private readonly BattleRules rules;
        private readonly IRandomSource random;
        private readonly BattleSide attacker;
        private readonly BattleSide defender;
        private readonly Dictionary<Ship, BattleSide> sideByShip = [];
        private readonly Dictionary<Ship, ShipTally> tallies = [];
        private readonly List<ShotRecord> shots = [];
        private readonly Dictionary<string, int> skipCounts = new(StringComparer.Ordinal);
        private readonly List<string> log = [];
        private readonly List<string> notes = [];
        private readonly Dictionary<Ship, double> startHp = [];
        private int reengagements;

        public Run(BattleScenario scenario, IRandomSource random)
        {
            this.scenario = scenario;
            defines = scenario.Defines;
            rules = scenario.Rules;
            this.random = random;

            if (ReferenceEquals(scenario.Attacker.Fleet, scenario.Defender.Fleet))
            {
                throw new ArgumentException("Attacker and defender must not share the same Fleet instance.");
            }

            attacker = new BattleSide("attacker", scenario.Attacker);
            defender = new BattleSide("defender", scenario.Defender);

            foreach (var side in new[] { attacker, defender })
            {
                foreach (var ship in side.Ships)
                {
                    ship.ResetCombatState();
                    sideByShip[ship] = side;
                    tallies[ship] = new ShipTally { StartExperience = ship.Experience };
                    startHp[ship] = ship.CurrentHP;
                }
            }

            AddInputNotes();
        }

        public BattleResult Execute(int seed)
        {
            var hour = 0;

            for (; hour < rules.MaxHours; hour++)
            {
                BeginHour(hour);
                var hourStartShots = shots.Count;
                ResolveSurfacePhase(hour);
                ResolveRetreatPhase(hour);
                ApplyExperienceGain(hour, hourStartShots);

                if (rules.DailyManpowerRecoveryInBattle && hour > 0 && hour % 24 == 0)
                {
                    foreach (var ship in sideByShip.Keys)
                    {
                        ExperienceRules.ApplyDailyManpowerRecovery(ship, defines);
                    }
                }

                LogHour(hour, hourStartShots);

                if (IsBattleOver(hour))
                {
                    hour++;
                    break;
                }
            }

            return BuildResult(seed, hour);
        }

        private void BeginHour(int hour)
        {
            attacker.Lines = new BattleLines(attacker.Ships);
            defender.Lines = new BattleLines(defender.Ships);
            attacker.Positioning = PositioningRules.Calculate(attacker.Lines, defender.Lines, defines);
            defender.Positioning = PositioningRules.Calculate(defender.Lines, attacker.Lines, defines);
            attacker.Screening = ScreeningRules.Calculate(attacker.Lines, attacker.Positioning, defines);
            defender.Screening = ScreeningRules.Calculate(defender.Lines, defender.Positioning, defines);
        }

        private void ResolveSurfacePhase(int hour)
        {
            var shooters = BuildFiringOrder();
            var simultaneous = rules.FiringOrder == FiringOrder.Simultaneous;
            var pending = new List<(Ship Shooter, Ship Target, double Damage, int ShotIndex)>();
            var isNight = IsNight(hour);
            var battleHasCarriers = attacker.Lines.Carriers.Count > 0 || defender.Lines.Carriers.Count > 0;
            var battleHasCapitals = attacker.Lines.Capitals.Count > 0 || defender.Lines.Capitals.Count > 0;

            bool IsActivelyFighting(Ship ship)
            {
                return hour >= WeaponCadenceRules.ActivationHour(ship.Role, battleHasCarriers, battleHasCapitals, defines, rules);
            }

            foreach (var shooter in shooters)
            {
                if (!shooter.IsInBattle)
                {
                    continue;
                }

                var ownSide = sideByShip[shooter];
                var enemySide = ReferenceEquals(ownSide, attacker) ? defender : attacker;
                var activationHour = WeaponCadenceRules.ActivationHour(shooter.Role, battleHasCarriers, battleHasCapitals, defines, rules);
                var stats = shooter.GetFinalStats();

                foreach (var weapon in WeaponOrder)
                {
                    var (attack, piercing) = weapon switch
                    {
                        WeaponType.Light => (stats.LightAttack, stats.LightPiercing),
                        WeaponType.Heavy => (stats.HeavyAttack, stats.HeavyPiercing),
                        WeaponType.Torpedo => (stats.TorpedoAttack, 0.0),
                        _ => (stats.DepthChargeAttack, 0.0)
                    };

                    // S-TARGET-SUB: submarines only use torpedoes.
                    if (attack <= 0 || (shooter.Role == ShipRole.Submarine && weapon != WeaponType.Torpedo))
                    {
                        continue;
                    }

                    // Depth charges are only used when submarines are present.
                    if (weapon == WeaponType.DepthCharge && !TargetingRules.ShipsInGroup(enemySide, GroupType.Submarine).Any())
                    {
                        continue;
                    }

                    var skipReason = WeaponCadenceRules.TryFire(shooter, weapon, hour, activationHour, defines, rules);

                    if (skipReason is not null)
                    {
                        CountSkip(skipReason);
                        continue;
                    }

                    var groups = TargetingRules.ReachableGroups(shooter, weapon, enemySide, ownSide.Participant.SubmarineEngagementRisk, random);
                    var target = TargetingRules.SelectTarget(shooter, weapon, groups, enemySide, IsActivelyFighting, defines, random);

                    if (target is null)
                    {
                        CountSkip(SkipReasons.NoTarget);
                        continue;
                    }

                    var hitChance = HitChanceRules.Calculate(shooter, target, weapon, ownSide.Screening, isNight, defines, rules);
                    var hit = random.NextDouble() < hitChance.Final;
                    var damage = hit
                        ? DamageRules.Calculate(shooter, target, weapon, attack, piercing, ownSide.Positioning, defines, rules, random)
                        : default;

                    var tally = tallies[shooter];
                    tally.ShotsFired++;

                    var record = new ShotRecord(
                        hour,
                        ownSide.Label,
                        shooter.ID,
                        weapon,
                        target.ID,
                        BattleLines.GetGroup(target.Role),
                        hitChance.Final,
                        hitChance.ProfileFactor,
                        hitChance.Modifier,
                        hit,
                        damage.Damage,
                        damage.PiercingTier,
                        damage.IsCritical,
                        damage.IsPartHit,
                        0,
                        0,
                        false);
                    shots.Add(record);

                    if (!hit)
                    {
                        continue;
                    }

                    tally.Hits++;
                    tally.DamageDealt += damage.Damage;

                    if (simultaneous)
                    {
                        pending.Add((shooter, target, damage.Damage, shots.Count - 1));
                    }
                    else
                    {
                        ApplyShotDamage(shooter, target, damage.Damage, shots.Count - 1, hour);
                    }
                }
            }

            foreach (var (shooter, target, damage, shotIndex) in pending)
            {
                ApplyShotDamage(shooter, target, damage, shotIndex, hour);
            }
        }

        private void ApplyShotDamage(Ship shooter, Ship target, double damage, int shotIndex, int hour)
        {
            if (target.IsSunk)
            {
                return;
            }

            var navy = defines.NNavy;
            var applied = DamageRules.Apply(target, damage, navy.COMBAT_DAMAGE_TO_STR_FACTOR, navy.COMBAT_DAMAGE_TO_ORG_FACTOR, defines);
            shots[shotIndex] = shots[shotIndex] with
            {
                HpDamage = applied.HpDamage,
                OrganizationDamage = applied.OrganizationDamage,
                KillingBlow = applied.Sunk
            };

            tallies[shooter].HpDamageDealt += applied.HpDamage;
            tallies[target].HpDamageTaken += applied.HpDamage;

            if (applied.Sunk)
            {
                tallies[target].SunkHour = hour;
            }
        }

        private List<Ship> BuildFiringOrder()
        {
            var attackerShips = attacker.Ships.Where(ship => ship.IsInBattle).OrderBy(ship => ship.ID, StringComparer.Ordinal);
            var defenderShips = defender.Ships.Where(ship => ship.IsInBattle).OrderBy(ship => ship.ID, StringComparer.Ordinal);
            var order = attackerShips.Concat(defenderShips).ToList();

            if (rules.FiringOrder == FiringOrder.AttackerFirst)
            {
                return order;
            }

            for (var i = order.Count - 1; i > 0; i--)
            {
                var j = random.Next(i + 1);
                (order[i], order[j]) = (order[j], order[i]);
            }

            return order;
        }

        private void ResolveRetreatPhase(int hour)
        {
            foreach (var side in new[] { attacker, defender })
            {
                foreach (var ship in side.Ships.OrderBy(ship => ship.ID, StringComparer.Ordinal))
                {
                    if (RetreatRules.AdvanceRetreat(ship, hour, side.Screening, defines))
                    {
                        tallies[ship].RetreatedHour = hour;
                        log.Add($"Hour {hour}: {side.Label} {ship.ID} escaped the battle");
                        continue;
                    }

                    if (rules.RetreatEnabled &&
                        RetreatRules.TryStartRetreat(ship, hour, side.Participant.RetreatPriority, defines, random))
                    {
                        log.Add($"Hour {hour}: {side.Label} {ship.ID} is retreating (HP {ship.CurrentHP:F1}/{ship.GetFinalStats().Hp:F1})");
                    }
                }
            }
        }

        private void ApplyExperienceGain(int hour, int hourStartShots)
        {
            var firedShips = shots.Skip(hourStartShots).Select(shot => (shot.ShooterSide, shot.ShooterId)).ToHashSet();

            foreach (var side in new[] { attacker, defender })
            {
                var enemy = ReferenceEquals(side, attacker) ? defender : attacker;
                var gain = ExperienceRules.HourlyGain(enemy.Lines.Count, side.Lines.Count, defines);

                foreach (var ship in side.Ships.Where(ship => firedShips.Contains((side.Label, ship.ID))))
                {
                    ExperienceRules.AddExperience(ship, gain, defines);
                }
            }
        }

        private bool IsBattleOver(int hour)
        {
            var attackerPresent = attacker.HasShipsInBattle;
            var defenderPresent = defender.HasShipsInBattle;

            if (attackerPresent && defenderPresent)
            {
                return false;
            }

            if (rules.ContinueAfterRetreat && TryReengage(attackerPresent, defenderPresent, hour))
            {
                return false;
            }

            return true;
        }

        private bool TryReengage(bool attackerPresent, bool defenderPresent, int hour)
        {
            var attackerAfloat = attacker.Ships.Any(ship => !ship.IsSunk);
            var defenderAfloat = defender.Ships.Any(ship => !ship.IsSunk);

            if (!attackerAfloat || !defenderAfloat)
            {
                return false;
            }

            foreach (var ship in sideByShip.Keys.Where(ship => !ship.IsSunk && ship.CurrentStatus == ShipStatus.Retreated))
            {
                ship.CurrentStatus = ShipStatus.Active;
                ship.RetreatProgress = 0;
                tallies[ship].RetreatedHour = null;
            }

            reengagements++;
            log.Add($"Hour {hour}: {(attackerPresent ? "defender" : "attacker")} fully retreated; re-engaging with all ships afloat");
            return true;
        }

        private bool IsNight(int hour)
        {
            var hardcoded = defines.Hardcoded;
            var timeOfDay = (rules.StartHourOfDay + hour) % 24;
            return hardcoded.NightStartHour > hardcoded.NightEndHour
                ? timeOfDay >= hardcoded.NightStartHour || timeOfDay < hardcoded.NightEndHour
                : timeOfDay >= hardcoded.NightStartHour && timeOfDay < hardcoded.NightEndHour;
        }

        private void CountSkip(string reason)
        {
            skipCounts[reason] = skipCounts.TryGetValue(reason, out var count) ? count + 1 : 1;
        }

        private void LogHour(int hour, int hourStartShots)
        {
            var hourShots = shots.Skip(hourStartShots).ToList();
            var timeOfDay = (rules.StartHourOfDay + hour) % 24;
            log.Add(
                $"Hour {hour} ({timeOfDay:D2}:00{(IsNight(hour) ? ", night" : string.Empty)}): " +
                $"{DescribeSide(attacker)} | {DescribeSide(defender)} | " +
                $"shots {hourShots.Count}, hits {hourShots.Count(shot => shot.Hit)}, " +
                $"HP dmg attacker->defender {hourShots.Where(shot => shot.ShooterSide == attacker.Label).Sum(shot => shot.HpDamage):F1}, " +
                $"defender->attacker {hourShots.Where(shot => shot.ShooterSide == defender.Label).Sum(shot => shot.HpDamage):F1}");
        }

        private static string DescribeSide(BattleSide side)
        {
            var lines = side.Lines;
            return $"{side.Label} S{lines.Screens.Count}/C{lines.Capitals.Count}/CV{lines.Carriers.Count}/SS{lines.Submarines.Count}" +
                   $"{(lines.Convoys.Count > 0 ? $"/CO{lines.Convoys.Count}" : string.Empty)} " +
                   $"pos {side.Positioning:P0} screen {side.Screening.ScreeningEfficiency:P0} cvscreen {side.Screening.CarrierScreeningEfficiency:P0}";
        }

        private void AddInputNotes()
        {
            var hasCarrierPlanes = new[] { scenario.Attacker, scenario.Defender }
                .Any(participant => participant.Fleet.CarrierAirwingsByShipDesign.Count > 0);
            var hasExternalPlanes = scenario.Attacker.ExternalNavalStrikePlanes > 0 || scenario.Defender.ExternalNavalStrikePlanes > 0;

            if (hasCarrierPlanes || hasExternalPlanes)
            {
                notes.Add("Air combat is not simulated yet (milestone 2): carrier air wings and land-based naval strike planes were ignored.");
            }

            if (!string.IsNullOrWhiteSpace(scenario.Weather) && !scenario.Weather.Equals("clear", StringComparison.OrdinalIgnoreCase))
            {
                notes.Add($"Weather '{scenario.Weather}' has no effect yet.");
            }

            if (sideByShip.Keys.Any(ship => ship.Role == ShipRole.Submarine))
            {
                notes.Add("Submarines are always targetable by depth charges; hide/reveal is not modelled yet.");
            }
        }

        private BattleResult BuildResult(int seed, int hoursFought)
        {
            var attackerPresent = attacker.HasShipsInBattle;
            var defenderPresent = defender.HasShipsInBattle;
            var outcome = (attackerPresent, defenderPresent) switch
            {
                (true, true) => BattleOutcome.Timeout,
                (true, false) => BattleOutcome.AttackerVictory,
                (false, true) => BattleOutcome.DefenderVictory,
                _ => BattleOutcome.BothSidesGone
            };

            var shipResults = sideByShip
                .OrderBy(entry => entry.Value.Label, StringComparer.Ordinal)
                .ThenBy(entry => entry.Key.ID, StringComparer.Ordinal)
                .Select(entry => BuildShipResult(entry.Key, entry.Value))
                .ToList();

            return new BattleResult
            {
                ScenarioId = scenario.ID,
                Seed = seed,
                Outcome = outcome,
                HoursFought = hoursFought,
                Reengagements = reengagements,
                Attacker = BuildSideResult(attacker),
                Defender = BuildSideResult(defender),
                Ships = shipResults,
                Shots = shots,
                SkipCounts = skipCounts,
                HourlyLog = log,
                Notes = notes
            };
        }

        private ShipResult BuildShipResult(Ship ship, BattleSide side)
        {
            var stats = ship.GetFinalStats();
            var tally = tallies[ship];
            return new ShipResult(
                side.Label,
                ship.ID,
                ship.Design.ID,
                ship.Role,
                stats.Hp,
                ship.CurrentHP,
                stats.Organization,
                ship.CurrentOrganization,
                ship.CurrentManpower,
                tally.StartExperience,
                ship.Experience,
                ship.CurrentStatus,
                ship.IsSunk,
                tally.SunkHour,
                tally.RetreatedHour,
                tally.ShotsFired,
                tally.Hits,
                tally.DamageDealt,
                tally.HpDamageDealt,
                tally.HpDamageTaken);
        }

        private SideResult BuildSideResult(BattleSide side)
        {
            return new SideResult(
                side.Label,
                side.Participant.Fleet.ID,
                side.Ships.Count,
                side.Ships.Count(ship => ship.IsSunk),
                side.Ships.Count(ship => !ship.IsSunk && ship.CurrentStatus == ShipStatus.Retreated),
                side.Ships.Count(ship => ship.IsInBattle),
                side.Ships.Sum(ship => startHp[ship]),
                side.Ships.Sum(ship => startHp[ship] - ship.CurrentHP),
                side.Ships.Sum(ship => tallies[ship].HpDamageDealt));
        }
    }

    private sealed class ShipTally
    {
        public double StartExperience;
        public int ShotsFired;
        public int Hits;
        public double DamageDealt;
        public double HpDamageDealt;
        public double HpDamageTaken;
        public int? SunkHour;
        public int? RetreatedHour;
    }
}
