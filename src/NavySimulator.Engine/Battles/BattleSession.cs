using NavySimulator.Engine.Battles.Air;
using NavySimulator.Engine.Battles.Mechanics;
using NavySimulator.Engine.Defines;
using NavySimulator.Engine.Leaders;
using NavySimulator.Engine.Random;
using NavySimulator.Engine.Results;

namespace NavySimulator.Engine.Battles;

/// <summary>
/// One battle that can be advanced hour by hour, in the order hoi4.exe 1.19.3 uses (S-HOUR, S-ORDER in docs/mechanics/surface.md):
/// 1. positioning and screening for both sides;
/// 2. the attacker's turn, then the defender's turn. In a turn every ship, in fleet order, steps its submarine reveal
///    timer, advances its state (waiting, fighting, retreating), decides whether to retreat, and fires its weapon slots
///    (heavy, torpedo, light) at one target per slot. Damage applies immediately, so ships sunk by the attacker do not
///    fire in the defender's turn. The side's air groups strike after its surface fire in air hours (A-*);
/// 3. experience gain, the daily manpower refill when the hour is 00:00, then the end-of-battle check.
/// "Tick" is the game's combat hour counter, which is 1 in battle hour 0.
/// The session owns the scenario's ships while it runs: do not run two sessions on the same scenario at once.
/// Stepping in any chunk sizes gives the same battle as <see cref="RunToEnd"/> for the same random source.
/// </summary>
public sealed class BattleSession
{
    private static readonly WeaponSlot[] SlotOrder = [WeaponSlot.Heavy, WeaponSlot.Torpedo, WeaponSlot.Light];

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
    private readonly List<BattleEvent> events = [];
    private readonly List<HourSummary> timeline = [];
    private readonly List<ShipHourState[]>? shipHistory;
    private readonly AirPhase airPhase;
    private int reengagements;
    private int hoursFought;
    private bool over;

    public BattleSession(BattleScenario scenario, int seed, bool recordShipHistory = false)
        : this(scenario, new SeededRandomSource(seed), seed, recordShipHistory)
    {
    }

    public BattleSession(BattleScenario scenario, IRandomSource random, int seed = 0, bool recordShipHistory = false)
    {
        this.scenario = scenario;
        defines = scenario.Defines;
        rules = scenario.Rules;
        this.random = random;
        Seed = seed;

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

        SetNavalDominance();
        airPhase = new AirPhase(scenario, attacker, defender);
        CreationUpdate();
        AddInputNotes();
        shipHistory = recordShipHistory ? [] : null;
        over = rules.MaxHours <= 0;
    }

    public BattleScenario Scenario => scenario;
    public int Seed { get; }

    /// <summary>Hours simulated so far; the next <see cref="StepHour"/> fights battle hour <c>HoursFought</c>.</summary>
    public int HoursFought => hoursFought;

    public bool IsOver => over;

    public BattleOutcome Outcome => over ? FinalOutcome() : BattleOutcome.InProgress;

    public BattleSide Attacker => attacker;
    public BattleSide Defender => defender;

    /// <summary>Attacker ships in fleet order, then defender ships; the index used by <see cref="ShipStateAt"/>.</summary>
    public IReadOnlyList<Ship> Ships => allShips;

    public IReadOnlyList<ShotRecord> Shots => shots;
    public IReadOnlyList<AirStrikeRecord> AirStrikes => airStrikes;

    /// <summary>The battle's air objects (A-*); a replay sets their planes from the game's log.</summary>
    public AirPhase AirPhase => airPhase;
    public IReadOnlyList<string> HourlyLog => log;
    public IReadOnlyList<BattleEvent> Events => events;
    public IReadOnlyList<HourSummary> Timeline => timeline;
    public IReadOnlyList<string> Notes => notes;
    public IReadOnlyDictionary<string, int> SkipCounts => skipCounts;
    public int Reengagements => reengagements;
    public bool RecordsShipHistory => shipHistory is not null;

    /// <summary>Hour of day (0-23) of battle hour <paramref name="hour"/>.</summary>
    public int HourOfDay(int hour)
    {
        return (rules.StartHourOfDay + hour) % 24;
    }

    public bool IsNight(int hour)
    {
        return Darkness(hour) > 0;
    }

    public string SideOf(Ship ship)
    {
        return sideByShip[ship].Label;
    }

    /// <summary>Ship <paramref name="shipIndex"/> (into <see cref="Ships"/>) at the end of battle hour <paramref name="hour"/>.</summary>
    public ShipHourState ShipStateAt(int hour, int shipIndex)
    {
        if (shipHistory is null)
        {
            throw new InvalidOperationException("This session does not record ship history.");
        }

        return shipHistory[hour][shipIndex];
    }

    /// <summary>Fights one battle hour. Returns false when the battle was already over.</summary>
    public bool StepHour()
    {
        if (over)
        {
            return false;
        }

        var hour = hoursFought;
        var tick = hour + 1;
        var hourStartShots = shots.Count;
        var hourStartStrikes = airStrikes.Count;
        JoinLandBasedPlanes(hour);
        BeginHour(hour);
        SideTurn(attacker, defender, hour, tick);
        SideTurn(defender, attacker, hour, tick);
        ApplyExperienceGain(hour);
        ApplyOrganizationRegain();
        LogHour(hour, hourStartShots);
        ApplyDailyManpowerGain(hour);
        ApplyDailyAirReinforcement(hour);
        RecordHour(hour, hourStartShots, hourStartStrikes);
        hoursFought++;

        if (IsBattleOver(hour) || hoursFought >= rules.MaxHours)
        {
            over = true;
        }

        return true;
    }

    /// <summary>Fights up to <paramref name="hours"/> hours, stopping when the battle ends. Returns the hours fought.</summary>
    public int Step(int hours)
    {
        var fought = 0;

        while (fought < hours && StepHour())
        {
            fought++;
        }

        return fought;
    }

    public void RunToEnd()
    {
        while (StepHour())
        {
        }
    }

    /// <summary>The result so far; the lists are copies, so the session can keep running.</summary>
    public BattleResult BuildResult()
    {
        var shipResults = sideByShip
            .OrderBy(entry => entry.Value.Label, StringComparer.Ordinal)
            .ThenBy(entry => entry.Key.ID, StringComparer.Ordinal)
            .Select(entry => BuildShipResult(entry.Key, entry.Value))
            .ToList();

        return new BattleResult
        {
            ScenarioId = scenario.ID,
            Seed = Seed,
            Outcome = Outcome,
            HoursFought = hoursFought,
            Reengagements = reengagements,
            Attacker = BuildSideResult(attacker),
            Defender = BuildSideResult(defender),
            Ships = shipResults,
            Shots = shots.ToList(),
            AirStrikes = airStrikes.ToList(),
            SkipCounts = new Dictionary<string, int>(skipCounts, StringComparer.Ordinal),
            HourlyLog = log.ToList(),
            Notes = notes.ToList(),
            Events = events.ToList(),
            Timeline = timeline.ToList()
        };
    }

    /// <summary>The ship's state and fire totals so far.</summary>
    public ShipResult ShipResultOf(Ship ship)
    {
        return BuildShipResult(ship, sideByShip[ship]);
    }

    public SideResult SideResultOf(BattleSide side)
    {
        return BuildSideResult(side);
    }

    /// <summary>
    /// [LOG] S-HOUR: the game updates a new battle once with hour counter 0 before its first battle hour: the side update
    /// (positioning, screening, fleet AA) runs and carriers gain their sortie experience, but nothing fires. Battle hour 0
    /// then starts from that screening, which the carrier sorties and so the carrier-ratio positioning read.
    /// </summary>
    private void CreationUpdate()
    {
        const int hour = -1;
        BeginHour(hour);

        foreach (var side in new[] { attacker, defender })
        {
            foreach (var ship in side.Ships)
            {
                AddCarrierSortieExperience(side, ship, hour);
            }
        }
    }

    // [RE] Positioning, screening and detection for both sides from the same pre-fire state; [CAL] the experience
    // level the combat modifiers use is taken here too (S-XP).
    private void BeginHour(int hour)
    {
        foreach (var ship in allShips)
        {
            ship.LevelExperience = ship.Experience;
        }

        attacker.Lines = new BattleLines(attacker.Ships);
        defender.Lines = new BattleLines(defender.Ships);

        // Carrier air strength uses the previous hour's screening, as the game computes positioning first.
        var attackerAir = airPhase.CarrierAirStrength(attacker, hour, Darkness(hour));
        var defenderAir = airPhase.CarrierAirStrength(defender, hour, Darkness(hour));
        attacker.Positioning = PositioningRules.Calculate(attacker.Lines, defender.Lines, defines, attackerAir.A, defenderAir,
            SideModifier(attacker, StaticModifiers.Positioning), FleetSizePenaltyFactor(defender));
        defender.Positioning = PositioningRules.Calculate(defender.Lines, attacker.Lines, defines, defenderAir.A, attackerAir,
            SideModifier(defender, StaticModifiers.Positioning), FleetSizePenaltyFactor(attacker));
        // [RE + LOG] A-AA-REDUCE: the fleet AA term is fixed here for the hour, so ships sunk during it still count.
        attacker.FleetAntiAir = AirRules.FleetAntiAir(attacker.Ships, attacker.Positioning, defines);
        defender.FleetAntiAir = AirRules.FleetAntiAir(defender.Ships, defender.Positioning, defines);
        attacker.Screening = ScreeningRules.Calculate(attacker.Lines, attacker.Positioning, defines,
            SideModifier(attacker, StaticModifiers.ScreeningEfficiency), Country(attacker, StaticModifiers.ScreeningWithoutScreens));
        defender.Screening = ScreeningRules.Calculate(defender.Lines, defender.Positioning, defines,
            SideModifier(defender, StaticModifiers.ScreeningEfficiency), Country(defender, StaticModifiers.ScreeningWithoutScreens));
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
                        events.Add(new BattleEvent(hour, own.Label, ship.ID, BattleEventKind.Activated,
                            ship.Role == ShipRole.Convoy ? "line activated, starts escaping" : "line activated, fires from next hour"));
                    }
                    else
                    {
                        CountSkip(SkipReasons.LineNotActive);
                    }

                    break;
                case ShipStatus.Active:
                    ship.RetreatProgress = 0;

                    if (rules.RetreatEnabled &&
                        RetreatRules.TryStartRetreat(ship, tick, own.Participant.Aggressiveness, defines, random,
                            enemy.Participant.Admiral?.TraitModifiers.Get(StaticModifiers.NavalEnemyRetreatChance) ?? 0))
                    {
                        log.Add($"Hour {hour}: {own.Label} {ship.ID} is retreating (HP {ship.CurrentHP:F1}/{ship.GetFinalStats().Hp:F1})");
                        events.Add(new BattleEvent(hour, own.Label, ship.ID, BattleEventKind.RetreatStarted,
                            $"decides to retreat (HP {ship.CurrentHP:F1}/{ship.GetFinalStats().Hp:F1}, org {ship.CurrentOrganization:F1})"));
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
                        events.Add(new BattleEvent(hour, own.Label, ship.ID, BattleEventKind.Escaped, "escaped the battle"));
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
        var hourOfDay = HourOfDay(hour);

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
                events.Add(new BattleEvent(hour, enemy.Label, target.ID, BattleEventKind.Sunk,
                    $"sunk by an air strike from {record.Source}{PrideNote(target)}", record.Source));
                ReinforceAfterSinking(enemy, hour);
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

            var target = TargetingRules.SelectTarget(shooter, slot, own, enemy, defines, random, Darkness(hour));

            if (target is null)
            {
                CountSkip(SkipReasons.NoTarget);
                continue;
            }

            FireAt(shooter, target, slot, own, enemy, hour);

            if (slot == WeaponSlot.Torpedo && shooter.Role == ShipRole.Submarine)
            {
                SubmarineRules.RevealAfterFiring(shooter, enemy.SubmarineDetection, own.Positioning, defines, random, Darkness(hour));
            }
        }
    }

    // [RE] 0x19740c0: one profile per slot and target; shoot while the slot has charge, stop when the target sinks.
    private void FireAt(Ship shooter, Ship target, WeaponSlot slot, BattleSide own, BattleSide enemy, int hour)
    {
        var criticalChanceFactor = DamageRules.CriticalChanceFactor(shooter, target, own.Screening, enemy.Screening, Darkness(hour), defines);
        var profile = DamageRules.Profile(shooter, target, slot, own.Positioning, defines, criticalChanceFactor, Darkness(hour), own.Screening,
            enemy.Screening);
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
            var hit = random.NextDouble(RollKind.Hit) < hitChance.Final;
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
                events.Add(new BattleEvent(hour, enemy.Label, target.ID, BattleEventKind.Sunk,
                    $"sunk by {shooter.ID} ({weapon}){PrideNote(target)}", shooter.ID));
                ReinforceAfterSinking(enemy, hour);
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
            events.Add(new BattleEvent(hour, own.Label, ship.ID, BattleEventKind.Disengaged, "disengages (only hidden submarines left)"));
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
                    ExperienceRules.AddExperience(ship, ExperienceRules.ModifiedGain(ship, gain, defines), defines);
                }
                else
                {
                    AddCarrierSortieExperience(side, ship, hour);
                }
            }
        }
    }

    // [RE] A carrier that did not fire gains from its sortie efficiency instead. [LOG] The efficiency is not clamped to 1:
    // TFB carriers at 1.55 in the first day gained 0.24789 an hour in game (0.16 per unit of efficiency).
    private void AddCarrierSortieExperience(BattleSide side, Ship ship, int hour)
    {
        if (!ship.IsInBattle || airPhase.GroupsOf(side).FirstOrDefault(group => group.Carrier == ship) is not { } group)
        {
            return;
        }

        var efficiency = Math.Max(0, airPhase.SortieEfficiency(group, side, hour, Darkness(hour)));
        var navy = defines.NNavy;
        var gain = navy.UNIT_EXPERIENCE_PER_COMBAT_HOUR * navy.UNIT_EXPERIENCE_SCALE * efficiency * navy.EXPERIENCE_FACTOR_CARRIER_GAIN;
        ExperienceRules.AddExperience(ship, ExperienceRules.ModifiedGain(ship, gain, defines), defines);
    }

    // S-MANPOWER [CAL]: a sinking refills the rest of the sunk ship's task force at once, as the daily update does
    // (a logged TFB battle: every refill outside 00:00 came with a sinking, and later shots that hour saw the new crews).
    private void ReinforceAfterSinking(BattleSide side, int hour)
    {
        var gained = side.Ships.Where(ship => !ship.IsSunk).Sum(ship => ExperienceRules.DailyManpowerGain(ship, defines));

        if (gained > 0)
        {
            log.Add($"Hour {hour}: {side.Label} ship sunk, the task force regains {gained} manpower");
        }
    }

    // S-ORG-REGAIN [CAL]: after the hour, retreating and escaped ships regain organization, more on the side with naval
    // dominance (S-DOMINANCE).
    private void ApplyOrganizationRegain()
    {
        foreach (var ship in allShips)
        {
            var dominance = sideByShip[ship].HasNavalDominance ? defines.NNavy.NAVAL_DOMINANCE_ORG_RECOVERY : 0;
            ExperienceRules.HourlyOrganizationRegain(ship, defines, dominance);
        }
    }

    // S-DOMINANCE [RE + ASSUMED]: each side's dominance from its ships at the start; the larger one (both on a tie) has
    // dominance for the whole battle.
    private void SetNavalDominance()
    {
        if (!rules.NavalDominanceEnabled)
        {
            return;
        }

        attacker.NavalDominance = NavalDominanceRules.SideDominance(attacker.Ships, defines);
        defender.NavalDominance = NavalDominanceRules.SideDominance(defender.Ships, defines);
        attacker.HasNavalDominance = NavalDominanceRules.IsDominant(attacker.NavalDominance, defender.NavalDominance);
        defender.HasNavalDominance = NavalDominanceRules.IsDominant(defender.NavalDominance, attacker.NavalDominance);
    }

    // S-MANPOWER [RE]: the daily update runs after the combat hour of the tick that starts a new day (00:00) and refills
    // every ship of the country, in battle or not; escaped ships are still in their task force, sunk ones are gone.
    private void ApplyDailyManpowerGain(int hour)
    {
        if (HourOfDay(hour) != 0)
        {
            return;
        }

        var gained = allShips.Where(ship => !ship.IsSunk).Sum(ship => ExperienceRules.DailyManpowerGain(ship, defines));

        if (gained > 0)
        {
            log.Add($"Hour {hour}: day change, ships regain {gained} manpower");
        }
    }

    // A-WINGS-EXT [RE]: the hourly air update runs before the combat: in air hours land-based planes join both sides.
    private void JoinLandBasedPlanes(int hour)
    {
        if (airPhase.HasAir && AirRules.IsAirHour(HourOfDay(hour), rules.AirRegionPhase, defines))
        {
            airPhase.JoinLandBased(hour);
        }
    }

    // A-REINFORCE [RE]: the daily update after the 00:00 combat hour refills land-based wings from the stockpile.
    private void ApplyDailyAirReinforcement(int hour)
    {
        if (HourOfDay(hour) != 0 || !airPhase.HasAir)
        {
            return;
        }

        foreach (var side in new[] { attacker, defender })
        {
            log.AddRange(airPhase.ReinforceDaily(side).Select(line => $"Hour {hour}: day change, {line}"));
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
        var retreatedSide = attackerPresent ? "defender" : "attacker";
        log.Add($"Hour {hour}: {retreatedSide} fully retreated; re-engaging with all ships afloat");
        events.Add(new BattleEvent(hour, retreatedSide, null, BattleEventKind.Reengaged, "fully retreated; re-engaging with all ships afloat"));
        return true;
    }

    // S-NIGHT [ASSUMED darkness model]: 1 during night hours, 0 otherwise. [CAL] With weather off the game's darkness
    // stays 0 (it comes from the weather manager), so there is no night.
    private double Darkness(int hour)
    {
        if (!rules.NightEnabled || !rules.WeatherEnabled)
        {
            return 0;
        }

        var hardcoded = defines.Hardcoded;
        var timeOfDay = HourOfDay(hour);
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
        var timeOfDay = HourOfDay(hour);
        log.Add(
            $"Hour {hour} ({timeOfDay:D2}:00{(Darkness(hour) > 0 ? ", night" : string.Empty)}): " +
            $"{DescribeSide(attacker)} | {DescribeSide(defender)} | " +
            $"shots {hourShots.Count}, hits {hourShots.Count(shot => shot.Hit)}, " +
            $"HP dmg attacker->defender {hourShots.Where(shot => shot.ShooterSide == attacker.Label).Sum(shot => shot.HpDamage):F1}, " +
            $"defender->attacker {hourShots.Where(shot => shot.ShooterSide == defender.Label).Sum(shot => shot.HpDamage):F1}");
    }

    private void RecordHour(int hour, int hourStartShots, int hourStartStrikes)
    {
        timeline.Add(new HourSummary(
            hour,
            HourOfDay(hour),
            Darkness(hour) > 0,
            DescribeHour(attacker, hourStartShots, hourStartStrikes),
            DescribeHour(defender, hourStartShots, hourStartStrikes)));

        shipHistory?.Add(allShips
            .Select(ship => new ShipHourState(ship.CurrentHP, ship.CurrentOrganization, ship.CurrentStatus, ship.Experience))
            .ToArray());
    }

    private SideHourState DescribeHour(BattleSide side, int hourStartShots, int hourStartStrikes)
    {
        var lines = side.Lines;
        var fired = 0;
        var hits = 0;
        var hpDamage = 0.0;
        var airHpDamage = 0.0;

        for (var i = hourStartShots; i < shots.Count; i++)
        {
            if (shots[i].ShooterSide == side.Label)
            {
                fired++;
                hits += shots[i].Hit ? 1 : 0;
                hpDamage += shots[i].HpDamage;
            }
        }

        for (var i = hourStartStrikes; i < airStrikes.Count; i++)
        {
            if (airStrikes[i].Side == side.Label)
            {
                airHpDamage += airStrikes[i].HpDamage;
            }
        }

        return new SideHourState(
            lines.Screens.Count,
            lines.Capitals.Count,
            lines.Carriers.Count,
            lines.Submarines.Count,
            lines.Convoys.Count,
            side.Positioning,
            side.Screening.ScreeningEfficiency,
            side.Screening.CarrierScreeningEfficiency,
            side.Ships.Count(ship => ship.IsInBattle),
            side.Ships.Count(ship => ship.IsSunk),
            side.Ships.Count(ship => !ship.IsSunk && ship.CurrentStatus == ShipStatus.Retreated),
            side.Ships.Sum(ship => Math.Max(0, ship.CurrentHP)),
            side.Ships.Where(ship => !ship.IsSunk).Sum(ship => Math.Max(0, ship.CurrentOrganization)),
            fired,
            hits,
            hpDamage + airHpDamage,
            airHpDamage);
    }

    private static string DescribeSide(BattleSide side)
    {
        var lines = side.Lines;
        return $"{side.Label} S{lines.Screens.Count}/C{lines.Capitals.Count}/CV{lines.Carriers.Count}/SS{lines.Submarines.Count}" +
               $"{(lines.Convoys.Count > 0 ? $"/CO{lines.Convoys.Count}" : string.Empty)} " +
               $"pos {side.Positioning:P0} screen {side.Screening.ScreeningEfficiency:P0} cvscreen {side.Screening.CarrierScreeningEfficiency:P0}";
    }

    // S-POS-MOD, S-SCREEN [RE] 0x15c70b0: side-level reads of a modifier, Mod(id; country, location, admiral): the country
    // and the side's commanding admiral's container A (S-ADMIRAL-MOD); the location part (region weather, naval terrain)
    // is not modelled.
    private static double SideModifier(BattleSide side, string key)
    {
        return side.Participant.CountryModifiers.Get(key) + (side.Participant.Admiral?.CombatModifiers.Get(key) ?? 0);
    }

    // [RE] 0x161d2d6: screening_without_screens is Mod with no location and no admiral, the country alone.
    private static double Country(BattleSide side, string key)
    {
        return side.Participant.CountryModifiers.Get(key);
    }

    // S-POS-SIZE [RE] 0x161bfdc: the enemy commanding admiral's naval_enemy_fleet_size_ratio_penalty_factor (container A only).
    private static double FleetSizePenaltyFactor(BattleSide enemy)
    {
        return enemy.Participant.Admiral?.CombatModifiers.Get(StaticModifiers.NavalEnemyFleetSizeRatioPenaltyFactor) ?? 0;
    }

    private static string PrideNote(Ship ship)
    {
        return ship.IsPrideOfTheFleet ? ", the pride of the fleet" : string.Empty;
    }

    private void AddInputNotes()
    {
        notes.AddRange(airPhase.Notes);

        if (rules.NavalDominanceEnabled)
        {
            var holders = new[] { attacker, defender }.Where(side => side.HasNavalDominance).Select(side => side.Label).ToList();
            notes.Add(FormattableString.Invariant($"Naval dominance at the start: attacker {attacker.NavalDominance:0.#}, defender {defender.NavalDominance:0.#}; ") +
                      (holders.Count == 0 ? "neither side has it." : $"{string.Join(" and ", holders)} regain{(holders.Count == 1 ? "s" : "")} organization with it."));
        }

        // S-WEATHER [RE toggle]: weather terms are not modelled yet.
        if (!string.IsNullOrWhiteSpace(scenario.Weather) && !scenario.Weather.Equals("clear", StringComparison.OrdinalIgnoreCase))
        {
            notes.Add(rules.WeatherEnabled
                ? $"Weather '{scenario.Weather}' has no effect yet."
                : $"Weather '{scenario.Weather}' is ignored: weather is off.");
        }

        foreach (var side in new[] { attacker, defender })
        {
            if (side.Participant.Admiral is { } admiral)
            {
                var levels = admiral.SkillLevels;
                notes.Add(FormattableString.Invariant(
                    $"{side.Label} admiral {admiral.Name}: skill {levels[LeaderSkills.Skill]}, attack {levels[LeaderSkills.Attack]}, defense {levels[LeaderSkills.Defense]}, ") +
                    FormattableString.Invariant($"maneuvering {levels[LeaderSkills.Maneuvering]}, coordination {levels[LeaderSkills.Coordination]}, ") +
                    (admiral.Traits.Count == 0 ? "no traits." : $"traits {string.Join(", ", admiral.Traits.Select(trait => trait.ID))}."));
            }

            // S-POTF: the commanding admiral's in-combat bonus is the only side-wide effect.
            if (side.Participant.PrideOfTheFleet is { } pride)
            {
                var commander = side.Participant.Admiral;
                var attack = commander?.CombatModifiers.Get(StaticModifiers.NavalHasPotfInCombatAttack) ?? 0;
                var defense = commander?.CombatModifiers.Get(StaticModifiers.NavalHasPotfInCombatDefense) ?? 0;
                notes.Add($"{side.Label} pride of the fleet: {pride.ID}" + (attack != 0 || defense != 0
                    ? FormattableString.Invariant($"; while it is in the battle the side's ships get attack {attack:+0.##%;-0.##%} and defense {defense:+0.##%;-0.##%} from the admiral.")
                    : "; the admiral gives no bonus for it."));
            }
        }
    }

    private BattleOutcome FinalOutcome()
    {
        return (attacker.HasShipsInBattle, defender.HasShipsInBattle) switch
        {
            (true, true) => BattleOutcome.Timeout,
            (true, false) => BattleOutcome.AttackerVictory,
            (false, true) => BattleOutcome.DefenderVictory,
            _ => BattleOutcome.BothSidesGone
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
            tally.HpDamageTaken,
            stats.ProductionCost);
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
