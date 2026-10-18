using NavySimulator.Engine.Battles.Air;
using NavySimulator.Engine.Battles.Mechanics;
using NavySimulator.Engine.Defines;
using NavySimulator.Engine.Random;
using NavySimulator.Engine.Results;

namespace NavySimulator.Engine.Battles;

/// <summary>
/// Runs a battle hour by hour, in the order hoi4.exe 1.19.3 uses (S-HOUR, S-ORDER in docs/mechanics/surface.md):
/// 1. positioning and screening for both sides;
/// 2. the attacker's turn, then the defender's turn. In a turn every ship, in fleet order, steps its submarine reveal
///    timer, advances its state (waiting, fighting, retreating), decides whether to retreat, and fires its weapon slots
///    (heavy, torpedo, light) at one target per slot. Damage applies immediately, so ships sunk by the attacker do not
///    fire in the defender's turn. Air strikes would follow each side's surface fire (milestone 2);
/// 3. experience gain, then the end-of-battle check.
/// "Tick" is the game's combat hour counter, which is 1 in battle hour 0.
/// </summary>
public sealed class BattleEngine
{
    private static readonly WeaponSlot[] SlotOrder = [WeaponSlot.Heavy, WeaponSlot.Torpedo, WeaponSlot.Light];

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
        private readonly List<Ship> allShips;
        private readonly Dictionary<Ship, BattleSide> sideByShip = [];
        private readonly Dictionary<Ship, ShipTally> tallies = [];
        private readonly List<ShotRecord> shots = [];
        private readonly Dictionary<string, int> skipCounts = new(StringComparer.Ordinal);
        private readonly List<string> log = [];
        private readonly List<string> notes = [];
        private readonly Dictionary<Ship, double> startHp = [];
        private readonly List<AirStrikeRecord> airStrikes = [];
        private readonly AirPhase airPhase;
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
            allShips = attacker.Ships.Concat(defender.Ships).ToList();

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

            airPhase = new AirPhase(scenario, attacker, defender);
            AddInputNotes();
        }

        public BattleResult Execute(int seed)
        {
            var hour = 0;

            for (; hour < rules.MaxHours; hour++)
            {
                var tick = hour + 1;
                var hourStartShots = shots.Count;
                BeginHour(hour);
                SideTurn(attacker, defender, hour, tick);
                SideTurn(defender, attacker, hour, tick);
                ApplyExperienceGain(hour);
                LogHour(hour, hourStartShots);

                if (IsBattleOver(hour))
                {
                    hour++;
                    break;
                }
            }

            return BuildResult(seed, hour);
        }

        // [RE] Positioning, screening and detection for both sides from the same pre-fire state.
        private void BeginHour(int hour)
        {
            attacker.Lines = new BattleLines(attacker.Ships);
            defender.Lines = new BattleLines(defender.Ships);

            // Carrier air strength uses the previous hour's screening, as the game computes positioning first.
            var attackerAir = airPhase.CarrierAirStrength(attacker, hour, Darkness(hour));
            var defenderAir = airPhase.CarrierAirStrength(defender, hour, Darkness(hour));
            attacker.Positioning = PositioningRules.Calculate(attacker.Lines, defender.Lines, defines, attackerAir.A, defenderAir);
            defender.Positioning = PositioningRules.Calculate(defender.Lines, attacker.Lines, defines, defenderAir.A, attackerAir);
            attacker.Screening = ScreeningRules.Calculate(attacker.Lines, attacker.Positioning, defines);
            defender.Screening = ScreeningRules.Calculate(defender.Lines, defender.Positioning, defines);
            attacker.SubmarineDetection = SubmarineRules.Detection(attacker.Ships, defines);
            defender.SubmarineDetection = SubmarineRules.Detection(defender.Ships, defines);

            foreach (var tally in tallies.Values)
            {
                tally.FiredThisHour = false;
            }
        }

        private void SideTurn(BattleSide own, BattleSide enemy, int hour, int tick)
        {
            // [RE] Reach decisions use the enemy's visible line counts taken at the start of this side's turn.
            own.EnemyCounts = EnemyLineCounts.Of(enemy.Ships);
            var darkness = Darkness(hour);

            foreach (var ship in own.Ships)
            {
                if (!ship.IsInBattle)
                {
                    continue;
                }

                if (ship.Role == ShipRole.Submarine)
                {
                    SubmarineRules.HourlyReveal(ship, enemy.SubmarineDetection, own.Positioning, defines, random);
                }

                switch (ship.CurrentStatus)
                {
                    case ShipStatus.Waiting:
                        if (WeaponCadenceRules.IsActivated(ship, tick, allShips, defines))
                        {
                            // [RE] Convoys start escaping as soon as their line activates.
                            ship.CurrentStatus = ship.Role == ShipRole.Convoy ? ShipStatus.Retreating : ShipStatus.Active;
                        }
                        else
                        {
                            CountSkip(SkipReasons.LineNotActive);
                        }

                        break;
                    case ShipStatus.Active:
                        ship.RetreatProgress = 0;

                        if (rules.RetreatEnabled &&
                            RetreatRules.TryStartRetreat(ship, tick, own.Participant.EngagementLevel, defines, random))
                        {
                            log.Add($"Hour {hour}: {own.Label} {ship.ID} is retreating (HP {ship.CurrentHP:F1}/{ship.GetFinalStats().Hp:F1})");
                            break;
                        }

                        FireSlots(ship, own, enemy, hour);
                        break;
                    case ShipStatus.Retreating:
                        FireSlots(ship, own, enemy, hour);

                        if (RetreatRules.AdvanceRetreat(ship, tick, own.Screening, darkness, defines))
                        {
                            tallies[ship].RetreatedHour = hour;
                            log.Add($"Hour {hour}: {own.Label} {ship.ID} escaped the battle");
                        }

                        break;
                }
            }

            var airActed = ResolveAirStep(own, enemy, hour, darkness);

            if (!airActed)
            {
                DisengageFromHiddenSubmarines(own, enemy, hour);
            }
        }

        // A-* [RE]: the side's air groups strike after its surface fire, in air hours only.
        private bool ResolveAirStep(BattleSide own, BattleSide enemy, int hour, double darkness)
        {
            var hourOfDay = (rules.StartHourOfDay + hour) % 24;

            if (airPhase.GroupsOf(own).Count == 0 || !AirRules.IsAirHour(hourOfDay, rules.AirRegionPhase, defines))
            {
                return false;
            }

            var strikes = airPhase.Strike(own, enemy, hour, darkness, random);

            foreach (var (record, target, applied) in strikes)
            {
                airStrikes.Add(record);
                tallies[target].HpDamageTaken += applied.HpDamage;

                var carrier = own.Ships.FirstOrDefault(ship => ship.ID == record.Source);

                if (carrier is not null)
                {
                    tallies[carrier].HpDamageDealt += applied.HpDamage;
                    tallies[carrier].DamageDealt += applied.HpDamage;
                }

                if (applied.Sunk)
                {
                    tallies[target].SunkHour = hour;
                }

                log.Add($"Hour {hour}: {own.Label} air strike from {record.Source}: {record.Flying} flying, {record.Intercepted} intercepted, " +
                        $"{record.ShotDownByAntiAir} shot down, {record.Hits} hits on {record.TargetId} for {record.HpDamage:F1} HP" +
                        (record.KillingBlow ? " (sunk)" : string.Empty));
            }

            return strikes.Count > 0;
        }

        private void FireSlots(Ship shooter, BattleSide own, BattleSide enemy, int hour)
        {
            var stats = shooter.GetFinalStats();

            foreach (var slot in SlotOrder)
            {
                var hasAttack = slot switch
                {
                    WeaponSlot.Heavy => stats.HeavyAttack > 0,
                    WeaponSlot.Torpedo => stats.TorpedoAttack > 0,
                    _ => stats.LightAttack > 0 || stats.DepthChargeAttack > 0
                };

                if (!hasAttack)
                {
                    continue;
                }

                if (!WeaponCadenceRules.Charge(shooter, slot, defines))
                {
                    CountSkip(SkipReasons.Cooldown);
                    continue;
                }

                var target = TargetingRules.SelectTarget(shooter, slot, own, enemy, defines, random);

                if (target is null)
                {
                    CountSkip(SkipReasons.NoTarget);
                    continue;
                }

                FireAt(shooter, target, slot, own, hour);

                if (slot == WeaponSlot.Torpedo && shooter.Role == ShipRole.Submarine)
                {
                    SubmarineRules.RevealAfterFiring(shooter, enemy.SubmarineDetection, own.Positioning, defines, random);
                }
            }
        }

        // [RE] 0x19740c0: one profile per slot and target; shoot while the slot has charge, stop when the target sinks.
        private void FireAt(Ship shooter, Ship target, WeaponSlot slot, BattleSide own, int hour)
        {
            var profile = DamageRules.Profile(shooter, target, slot, own.Positioning, defines);
            var hitChance = HitChanceRules.Calculate(shooter, target, slot, own.Screening, Darkness(hour), defines);
            var weapon = profile.IsDepthCharge ? WeaponType.DepthCharge : slot switch
            {
                WeaponSlot.Heavy => WeaponType.Heavy,
                WeaponSlot.Torpedo => WeaponType.Torpedo,
                _ => WeaponType.Light
            };
            var tally = tallies[shooter];

            while (WeaponCadenceRules.TrySpendShot(shooter, slot, defines))
            {
                var hit = random.NextDouble() < hitChance.Final;
                var damage = hit ? DamageRules.RollHit(target, profile, defines, rules, random) : default;
                var applied = damage.Damage > 0 ? DamageRules.Apply(target, damage.StrDamage, damage.OrgDamage, defines) : default;

                tally.ShotsFired++;
                tally.FiredThisHour = true;
                tally.LastTargetWasConvoy = target.Role == ShipRole.Convoy;

                if (hit)
                {
                    tally.Hits++;
                    tally.DamageDealt += damage.Damage;
                    tally.HpDamageDealt += applied.HpDamage;
                    tallies[target].HpDamageTaken += applied.HpDamage;
                }

                if (applied.Sunk)
                {
                    tallies[target].SunkHour = hour;
                }

                shots.Add(new ShotRecord(
                    hour,
                    own.Label,
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
                    applied.HpDamage,
                    applied.OrganizationDamage,
                    applied.Sunk));

                if (target.IsSunk)
                {
                    break;
                }
            }
        }

        // [RE] A side that has nothing it can target (the enemy only has hidden submarines left) starts retreating.
        private void DisengageFromHiddenSubmarines(BattleSide own, BattleSide enemy, int hour)
        {
            var enemyInBattle = enemy.Ships.Where(ship => ship.IsInBattle).ToList();

            if (enemyInBattle.Count == 0 || enemyInBattle.Any(ship => ship.IsVisible))
            {
                return;
            }

            foreach (var ship in own.Ships.Where(ship => ship.IsInBattle && ship.CurrentStatus != ShipStatus.Retreating))
            {
                ship.CurrentStatus = ShipStatus.Retreating;
                ship.RetreatProgress = 0;
                log.Add($"Hour {hour}: {own.Label} {ship.ID} disengages (only hidden submarines left)");
            }
        }

        // S-XP [RE]: ships that fired gain experience; group sizes count every ship the side brought.
        private void ApplyExperienceGain(int hour)
        {
            foreach (var side in new[] { attacker, defender })
            {
                var enemy = ReferenceEquals(side, attacker) ? defender : attacker;

                foreach (var ship in side.Ships)
                {
                    var tally = tallies[ship];

                    if (tally.FiredThisHour)
                    {
                        var gain = ExperienceRules.HourlyGain(enemy.Ships.Count, side.Ships.Count, tally.LastTargetWasConvoy, defines);
                        ExperienceRules.AddExperience(ship, gain, defines);
                    }
                    else if (ship.IsInBattle && airPhase.GroupsOf(side).FirstOrDefault(group => group.Carrier == ship) is { } group)
                    {
                        // [RE] A carrier that did not fire gains from its sortie efficiency instead.
                        var efficiency = Math.Clamp(airPhase.SortieEfficiency(group, side, hour, Darkness(hour)), 0, 1);
                        var navy = defines.NNavy;
                        ExperienceRules.AddExperience(ship,
                            navy.UNIT_EXPERIENCE_PER_COMBAT_HOUR * navy.UNIT_EXPERIENCE_SCALE * efficiency * navy.EXPERIENCE_FACTOR_CARRIER_GAIN, defines);
                    }
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

            return !(rules.ContinueAfterRetreat && TryReengage(attackerPresent, hour));
        }

        private bool TryReengage(bool attackerPresent, int hour)
        {
            if (!attacker.Ships.Any(ship => !ship.IsSunk) || !defender.Ships.Any(ship => !ship.IsSunk))
            {
                return false;
            }

            foreach (var ship in allShips.Where(ship => !ship.IsSunk && ship.CurrentStatus == ShipStatus.Retreated))
            {
                ship.CurrentStatus = ShipStatus.Active;
                ship.RetreatProgress = 0;
                tallies[ship].RetreatedHour = null;
            }

            reengagements++;
            log.Add($"Hour {hour}: {(attackerPresent ? "defender" : "attacker")} fully retreated; re-engaging with all ships afloat");
            return true;
        }

        // S-NIGHT [ASSUMED darkness model]: 1 during night hours, 0 otherwise.
        private double Darkness(int hour)
        {
            if (!rules.NightEnabled)
            {
                return 0;
            }

            var hardcoded = defines.Hardcoded;
            var timeOfDay = (rules.StartHourOfDay + hour) % 24;
            var night = hardcoded.NightStartHour > hardcoded.NightEndHour
                ? timeOfDay >= hardcoded.NightStartHour || timeOfDay < hardcoded.NightEndHour
                : timeOfDay >= hardcoded.NightStartHour && timeOfDay < hardcoded.NightEndHour;
            return night ? 1 : 0;
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
                $"Hour {hour} ({timeOfDay:D2}:00{(Darkness(hour) > 0 ? ", night" : string.Empty)}): " +
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
            notes.AddRange(airPhase.Notes);

            if (!string.IsNullOrWhiteSpace(scenario.Weather) && !scenario.Weather.Equals("clear", StringComparison.OrdinalIgnoreCase))
            {
                notes.Add($"Weather '{scenario.Weather}' has no effect yet.");
            }

            if (!string.IsNullOrWhiteSpace(scenario.Attacker.Commander) || !string.IsNullOrWhiteSpace(scenario.Defender.Commander))
            {
                notes.Add("Admirals are not simulated yet.");
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
                AirStrikes = airStrikes,
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
        public bool FiredThisHour;
        public bool LastTargetWasConvoy;
    }
}
