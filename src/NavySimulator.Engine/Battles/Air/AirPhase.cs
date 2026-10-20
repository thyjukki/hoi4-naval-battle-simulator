using NavySimulator.Engine.Battles.Mechanics;
using NavySimulator.Engine.Defines;
using NavySimulator.Engine.Environment;
using NavySimulator.Engine.Random;
using NavySimulator.Engine.Results;
using NavySimulator.Engine.Stats;

namespace NavySimulator.Engine.Battles.Air;

/// <summary>
/// [RE] A-*: the air step of one side, run after that side's surface fire in air hours (0x1c6c580 / 0x197b890).
/// Spec: docs/mechanics/air.md.
/// </summary>
public sealed class AirPhase
{
    private readonly BattleScenario scenario;
    private readonly GameDefines defines;
    private readonly Dictionary<BattleSide, List<AirGroup>> groups = [];
    private readonly Dictionary<BattleSide, int> externalJoined = [];
    private readonly Dictionary<BattleSide, Dictionary<string, int>> stockpiles = [];

    public AirPhase(BattleScenario scenario, BattleSide attacker, BattleSide defender)
    {
        this.scenario = scenario;
        defines = scenario.Defines;
        Attacker = attacker;

        foreach (var side in new[] { attacker, defender })
        {
            groups[side] = BuildGroups(side);
            externalJoined[side] = 0;
            stockpiles[side] = new Dictionary<string, int>(side.Participant.PlaneStockpile, StringComparer.Ordinal);
        }
    }

    public BattleSide Attacker { get; }
    public List<string> Notes { get; } = [];

    /// <summary>Hourly log lines of the air step (aces earned and lost); the session moves them into its log.</summary>
    public List<string> Log { get; } = [];

    public IReadOnlyList<AirGroup> GroupsOf(BattleSide side)
    {
        return groups[side];
    }

    public bool HasAir => groups.Values.Any(list => list.Count > 0);

    /// <summary>A-REINFORCE: the side's planes left in stock, by plane ID.</summary>
    public IReadOnlyDictionary<string, int> StockpileOf(BattleSide side)
    {
        return stockpiles[side];
    }

    /// <summary>A-WINGS-STRIKE: all planes on a carrier, over its wing pools (deck crowding counts them all).</summary>
    public int CarrierPlanes(Ship carrier)
    {
        return groups.Values.SelectMany(list => list).Where(group => group.Carrier == carrier).Sum(group => group.Planes);
    }

    private List<AirGroup> BuildGroups(BattleSide side)
    {
        var result = new List<AirGroup>();
        var assignments = side.Participant.Fleet.CarrierAirwingsByShip;

        foreach (var carrier in side.Ships.Where(ship => ship.Role == ShipRole.Carrier))
        {
            if (!assignments.TryGetValue(carrier.ID, out var carrierAssignments))
            {
                continue;
            }

            var wings = new List<AirWing>();
            var executes = scenario.Rules.CarrierWingsExecuteNavalStrike;

            foreach (var assignment in carrierAssignments)
            {
                if (!scenario.PlanesByID.TryGetValue(assignment.PlaneID, out var plane))
                {
                    Notes.Add($"Unknown plane '{assignment.PlaneID}' on {carrier.ID}; wing ignored.");
                    continue;
                }

                for (var i = 0; i < assignment.Airwings; i++)
                {
                    wings.Add(new AirWing(plane, assignment.PlaneCount, defines.Hardcoded, executes, side.Participant.PlaneStatsOf(plane)));
                }
            }

            // [RE] A-WINGS-STRIKE (0x1c6a7c0): one air object per wing pool (plane sub-unit) on the carrier, in order of first wing.
            foreach (var pool in wings.GroupBy(wing => wing.Plane.Pool))
            {
                result.Add(AirGroup.ForCarrier(carrier, pool.ToList(), pool.Key));
            }
        }

        // A-EXTERNAL: one air object per land-based entry (the game's per air base AirFX); it strikes once it has joined.
        // [RE] A-WINGS-EXT: air superiority wings are never run through the naval join (0xf82110); they fight only in the
        // region's air war (A-REGION). Their groups never strike (no strike wings) and never join.
        foreach (var landBased in side.Participant.LandBasedAirwings.Where(entry => entry.Planes > 0))
        {
            if (scenario.PlanesByID.TryGetValue(landBased.PlaneID, out var plane))
            {
                result.Add(AirGroup.External([new AirWing(plane, landBased.Planes, defines.Hardcoded, stats: side.Participant.PlaneStatsOf(plane))], landBased.DisruptionFactor, landBased));
            }
            else
            {
                Notes.Add($"{side.Label}: {landBased.Planes} land-based planes of unknown type '{landBased.PlaneID}' ignored.");
            }
        }

        return result;
    }

    public double SortieEfficiency(AirGroup group, BattleSide side, int hour, BattleEnvironment environment)
    {
        return AirRules.SortieEfficiency(group, side.Screening, ReferenceEquals(side, Attacker), StanceIndex(side), hour, environment, defines,
            side.Participant.CarrierNightTraffic, CarrierPlanes(group.Carrier!));
    }

    private int StanceIndex(BattleSide side)
    {
        return side.Participant.CarrierStanceIndex ?? (int)defines.NNavy.CARRIER_OFFENSIVE_STANCE_DEFAULT_INDEX;
    }

    /// <summary>
    /// [RE] S-POS carrier-ratio inputs (0x161eeb0), over the carrier air objects (wing pools): A = sum of sortie
    /// efficiency * pool planes, B = mean of sortie efficiency * min(1, carrier planes / CARRIER_PLANES_AMOUNT_FOR_POSITIONING).
    /// </summary>
    public (double A, double B) CarrierAirStrength(BattleSide side, int hour, BattleEnvironment environment)
    {
        // [RE + LOG] 0x161eeb0 walks every carrier air object of the side's groups: a sunk carrier's stay (sortie 0, but
        // they count in B's mean); an escaped carrier's leave with it.
        var carrierGroups = groups[side].Where(group => group.Carrier is { } carrier && (carrier.IsSunk || carrier.IsInBattle)).ToList();

        if (carrierGroups.Count == 0)
        {
            return (0, 0);
        }

        var a = 0.0;
        var b = 0.0;

        foreach (var group in carrierGroups)
        {
            var efficiency = group.Carrier!.IsSunk ? 0 : SortieEfficiency(group, side, hour, environment);
            a += efficiency * group.PlanesCounted;
            b += efficiency * Math.Min(1.0, CarrierPlanes(group.Carrier!) / defines.NAir.CARRIER_PLANES_AMOUNT_FOR_POSITIONING);
        }

        return (a, b / carrierGroups.Count);
    }

    public List<(AirStrikeRecord Record, Ship Target, AppliedDamage Applied)> Strike(
        BattleSide own,
        BattleSide enemy,
        int hour,
        BattleEnvironment environment,
        IRandomSource random)
    {
        var results = new List<(AirStrikeRecord, Ship, AppliedDamage)>();
        var ownGroups = groups[own];

        if (ownGroups.Count == 0)
        {
            return results;
        }

        // [RE + LOG] The stacking factor is taken before the air objects recount their planes (0x1c6c580).
        var ownCarrierPlanes = ownGroups.Where(group => !group.IsExternal).Sum(group => group.PlanesCounted);
        var stacking = AirRules.StackingFactor(ownCarrierPlanes, AirRules.PlaneCap(enemy.Ships, defines), defines);

        foreach (var group in ownGroups)
        {
            group.CountedPlanes = group.Planes;
            var strikeWings = group.StrikeWings.ToList();

            if (strikeWings.Count == 0 || !group.HasJoined || group.Carrier is { IsInBattle: false })
            {
                continue;
            }

            // [RE] Only the first round(n * F) strike wings, of those executing naval strike, feed the averages (0x1978e30).
            var selected = strikeWings.Take(Math.Max(1, (int)Math.Round(strikeWings.Count * stacking, MidpointRounding.AwayFromZero)))
                .Where(wing => wing.ExecutesNavalStrike)
                .ToList();
            // [RE + LOG] A land-based group's mission efficiency (its disruption) weighs each wing's attack in the average,
            // and the group's own factor (airfx+0xc0, the same disruption) multiplies the result again (A-DMG).
            var (attack, targeting, agility) = AirRules.StrikeAverages(selected, defines, own.Participant.CountryModifiers, !group.IsExternal,
                group.Planes, group.IsExternal ? group.DisruptionFactor : 1.0);
            var flying = group.IsExternal
                ? group.Planes
                : (int)Math.Round(group.Planes * Math.Clamp(SortieEfficiency(group, own, hour, environment), 0, 1), MidpointRounding.AwayFromZero);

            if (flying <= 0)
            {
                continue;
            }

            var target = PickTarget(enemy, random);

            if (target is null)
            {
                continue;
            }

            var source = group.Source;
            // [RE + LOG] 0x197bb9d: every fighter-type wing on every carrier of the target's group that is still afloat,
            // escaped carriers and wings without planes included.
            var defenders = groups[enemy]
                .Where(defending => defending.Carrier is { IsSunk: false })
                .SelectMany(defending => defending.FighterWings)
                .ToList();
            var ratios = defines.NNavy.CARRIER_OFFENSIVE_STANCE_SORTIE_RATIO;
            var defendingShare = 1.0 - (ratios.Length == 0 ? 0.5 : ratios[Math.Clamp(StanceIndex(enemy), 0, ratios.Length - 1)]);
            var intercepted = Math.Min(flying, AirRules.Interception(strikeWings, defenders, defendingShare, defines, random));
            RemoveLosses(group, intercepted);

            var targetAntiAir = target.Role == ShipRole.Convoy ? 0 : CombatStatRules.AntiAir(target, defines);
            var inReach = (int)Math.Ceiling(Math.Max(0, flying - intercepted) * stacking);
            var shotDown = Math.Min(flying - intercepted, AirRules.PreemptiveAntiAir(targetAntiAir, agility, inReach, defines, random));
            RemoveLosses(group, shotDown);
            group.CountedPlanes = group.Planes;

            var strikers = flying - intercepted - shotDown;
            var hits = strikers > 0 ? AirRules.Hits(strikers, targeting, defines, random, AirRules.TargetingMultiplier(environment, defines)) : 0;
            var reduction = AirRules.AntiAirReduction(targetAntiAir, enemy.FleetAntiAir, defines);

            if (hits <= 0)
            {
                results.Add((new AirStrikeRecord(hour, own.Label, source, target.ID, flying, intercepted, shotDown, 0, reduction, false, 0, 0, false),
                    target, default));
                RollAces(own, group, intercepted + shotDown, 0, hour, random);
                continue;
            }

            var perPlaneAttack = attack * (group.IsExternal ? group.DisruptionFactor : defines.NAir.NAVAL_STRIKE_CARRIER_MULTIPLIER);
            var damage = hits * perPlaneAttack * (1.0 - reduction);
            var strDamage = damage * defines.NAir.NAVAL_STRIKE_DAMAGE_TO_STR;
            var orgDamage = damage * defines.NAir.NAVAL_STRIKE_DAMAGE_TO_ORG;
            // [RE] 0x197af20: the target's critical_receive_chance through the member getter (its side's screening bonuses).
            var receive = MemberModifierRules.Get(target, StaticModifiers.CriticalReceiveChance, enemy.Screening, environment, defines);
            var critical = RollCritical(target, hits, receive, ref strDamage, ref orgDamage, random);
            var applied = DamageRules.Apply(target, strDamage, orgDamage, defines);

            results.Add((new AirStrikeRecord(hour, own.Label, source, target.ID, flying, intercepted, shotDown, hits, reduction, critical,
                applied.HpDamage, applied.OrganizationDamage, applied.Sunk), target, applied));
            RollAces(own, group, intercepted + shotDown, (int)Math.Floor(applied.HpDamage + 1e-9), hour, random);
        }

        return results;
    }

    /// <summary>
    /// [RE] A-ACE-ROLLS (0x197c830, 0xf80f60): after a strike that reached its target, every wing of the group's strike
    /// list, in pool order, with the group's losses (intercepted + AA) and the target's HP lost (truncated): a live ace
    /// may die if planes were lost; a wing without a live ace may earn one if it did damage and lost fewer planes than
    /// that. A new ace's template is drawn by <c>chance</c> among the templates whose types meet the wing's sub-unit's.
    /// </summary>
    private void RollAces(BattleSide own, AirGroup group, int losses, int damage, int hour, IRandomSource random)
    {
        if (scenario.AceTemplates.Count == 0)
        {
            return;
        }

        var generation = own.Participant.CountryModifiers.Get(StaticModifiers.AirAceGenerationChanceFactor);

        foreach (var wing in group.StrikeWings)
        {
            if (wing.Ace is { Alive: true } ace)
            {
                if (losses > 0 && random.NextDouble(RollKind.AirAceDeath) <= AirRules.AceDeathChance(losses, defines))
                {
                    ace.Alive = false;
                    Log.Add($"Hour {hour}: {own.Label} {group.Source}: the {ace.Template.ID} ace of a {wing.Plane.ID} wing died.");
                }

                continue;
            }

            if (damage <= 0 || losses >= damage || random.NextDouble(RollKind.AirAceEarn) > AirRules.AceEarnChance(damage, generation, defines))
            {
                continue;
            }

            var types = wing.Plane.SubUnitTypes.Count > 0 ? wing.Plane.SubUnitTypes : wing.Plane.Types;
            var candidates = scenario.AceTemplates.Where(template => template.Types.Overlaps(types) && template.Chance > 0).ToList();

            if (candidates.Count == 0)
            {
                continue;
            }

            var point = random.NextDouble(RollKind.AirAceTemplate) * candidates.Sum(template => template.Chance);
            var chosen = candidates[^1];

            foreach (var template in candidates)
            {
                point -= template.Chance;

                if (point < 0)
                {
                    chosen = template;
                    break;
                }
            }

            // [ASSUMED] A new ace replaces a dead one (attaching 0xf5b320 detaches the old ace).
            wing.Ace = new AirAce(chosen, hour);
            Log.Add($"Hour {hour}: {own.Label} {group.Source}: a {wing.Plane.ID} wing earned a {chosen.ID} ace.");
        }
    }

    private Ship? PickTarget(BattleSide enemy, IRandomSource random)
    {
        var candidates = enemy.Ships
            .Where(ship => ship.IsInBattle && ship.IsVisible)
            .Select(ship => (ship, AirRules.TargetWeight(ship, enemy.Screening.ScreeningEfficiency, defines)))
            .Where(candidate => candidate.Item2 > 0)
            .ToList();
        return AirRules.PickTarget(candidates, random);
    }

    // [RE] A-CRIT (0x197ae86..0x197b06c): on a crit whose multiplier is above 1, a part is damaged as for guns
    // (0x1975150: S-CRIT-PART with CHANCE_TO_DAMAGE_PART_ON_CRITICAL_HIT_FROM_AIR); the part's multipliers then replace
    // the critical multiplier. Without part data or a candidate left, the multiplier applies.
    private bool RollCritical(Ship target, int hits, double criticalReceiveChance, ref double strDamage, ref double orgDamage, IRandomSource random)
    {
        var chance = AirRules.CriticalChance(target, criticalReceiveChance, defines);

        if (chance <= 0 || random.NextDouble(RollKind.AirCritical) >= chance)
        {
            return false;
        }

        var multiplier = AirRules.CriticalMultiplier(target, hits, defines);

        if (multiplier <= 1 || (strDamage <= 0 && orgDamage <= 0))
        {
            return true;
        }

        var part = CriticalPartRules.RollPartDamage(target, defines, random, defines.NNavy.CHANCE_TO_DAMAGE_PART_ON_CRITICAL_HIT_FROM_AIR)
            ? CriticalPartRules.DamageRandomPart(target, defines, random)
            : null;

        if (part is not null)
        {
            strDamage = strDamage * part.StrDamageMultiplier + part.StrDamage;
            orgDamage = orgDamage * part.OrgDamageMultiplier + part.OrgDamage;
        }
        else
        {
            strDamage *= multiplier;
            orgDamage *= multiplier;
        }

        return true;
    }

    // [RE] A-LOSSES (0x197a1dc): losses go to the strike wings executing naval strike, in order [ASSUMED]; an external
    // wing loses at most the planes it offered at its last join.
    private static void RemoveLosses(AirGroup group, int losses)
    {
        var limit = group.IsExternal ? Math.Min(losses, group.Offered) : losses;
        var removed = Math.Min(limit, group.StrikeWings.Where(wing => wing.ExecutesNavalStrike).Sum(wing => wing.Planes));
        AirRules.RemoveLosses(group.StrikeWings.Where(wing => wing.ExecutesNavalStrike).ToList(), removed);

        if (group.IsExternal)
        {
            group.Offered -= removed;
        }
    }

    /// <summary>
    /// [RE] A-WINGS-EXT (0xc57d30, 0x16205c0): at the start of an air hour, before the combat, each side's join count is
    /// reset and its land-based wings offer round(planes x mission efficiency) planes, in listed order [ASSUMED], while
    /// the side's join cap has room. A wing that joins is in the battle from then on and strikes with all its planes.
    /// </summary>
    public void JoinLandBased(int hour, RegionAirConditions conditions = default)
    {
        foreach (var (own, enemy) in new[] { (Attacker, Defender), (Defender, Attacker) })
        {
            externalJoined[own] = 0;
            var cap = (int)AirRules.ExternalJoinCap(enemy.Ships, hour, defines);

            foreach (var external in groups[own].Where(group => group.LandBased is { JoinsNavalBattle: true }))
            {
                if (cap <= 0 || externalJoined[own] >= cap)
                {
                    break;
                }

                var budget = (int)Math.Round(external.Planes * LandEffectiveness(own, external.LandBased!, conditions), MidpointRounding.AwayFromZero);
                var add = Math.Clamp(Math.Min(budget, cap - externalJoined[own]), 0, int.MaxValue);
                externalJoined[own] += add;
                external.Offered = Math.Min(add, external.Planes);
                external.HasJoined = true;

                // [RE] 0x197a6a0: airfx+0xc0 = 0xf78160 of the mission's disruption with the offered planes (A-REGION-DISRUPT),
                // times the scenario's own factor.
                if (RegionAirWarEnabled && external.Wings.Count > 0)
                {
                    external.DisruptionFactor = external.LandBased.DisruptionFactor *
                                                RegionAirWarRules.StrikeEfficiency(external.Wings[0].Disruption, budget, external.Wings[0], defines,
                                                    own.Participant.CountryModifiers);
                }
            }
        }
    }

    /// <summary>
    /// A-REGION: whether the region's air war runs (a replay turns it off and takes each wing's planes and disruption from
    /// the game's log instead).
    /// </summary>
    public bool RegionAirWarEnabled { get; set; } = true;

    private readonly Dictionary<BattleSide, double> detection = new();

    /// <summary>A-DETECT: each side's air detection in the region in the last air hour's region air war.</summary>
    public IReadOnlyDictionary<BattleSide, double> RegionDetection => detection;

    private sealed class RegionEntry(AirWing wing, AirGroup group, bool carrier, bool strike, bool airSuperiority, int planes)
    {
        public AirWing Wing { get; } = wing;
        public AirGroup Group { get; } = group;
        public bool Carrier { get; } = carrier;
        public bool Strike { get; } = strike;
        public bool AirSuperiority { get; } = airSuperiority;

        /// <summary>effective_planes_count: the mission's planes this hour, and its loss budget.</summary>
        public int Planes { get; } = planes;

        public int Left { get; set; }
    }

    /// <summary>
    /// [RE + LOG] A-REGION (0xc57d30 → 0xc5f4c0, 0xf77360, 0xc4c570, 0xf87550): the region's air war of an air hour, before
    /// the naval combat hour. Every wing's disruption and losses are reset; each side in turn (the combat's attacker
    /// first, [ASSUMED]) attacks the other's wings with its region missions (all its wings, when its carrier wings fly a
    /// region mission, and its land-based wings) and its scrambled carrier fighters; then escorts cancel their side's
    /// disruption, and the losses are taken off the wings (a carrier wing left without planes is deleted, [LOG]).
    /// Each side's air detection in the region (A-DETECT) comes from its region missions, its radar coverage and the
    /// <paramref name="conditions"/> of the hour. Not modelled: weather, night and mission efficiency modifiers of the
    /// stats, the distance weight, accidents, aces in air-to-air combat. Spec: air.md A-REGION, A-DETECT.
    /// </summary>
    public void RegionAirWar(int hour, IRandomSource random, bool applyLosses = true, RegionAirConditions conditions = default)
    {
        if (!RegionAirWarEnabled)
        {
            return;
        }

        foreach (var wing in groups.Values.SelectMany(list => list).SelectMany(group => group.Wings))
        {
            wing.RegionLosses = 0;
            wing.DisruptionTaken = 0;
            wing.Disruption = 0;
            wing.RegionMission = null;
        }

        var lists = new Dictionary<BattleSide, (List<RegionEntry> RegionList, List<RegionEntry> Scrambled)>();

        foreach (var side in new[] { Attacker, Defender })
        {
            var regionList = new List<RegionEntry>();
            var scrambled = new List<RegionEntry>();
            var ratios = defines.NNavy.CARRIER_OFFENSIVE_STANCE_SORTIE_RATIO;
            var ratio = ratios.Length == 0 ? 1.0 : ratios[Math.Clamp(StanceIndex(side), 0, ratios.Length - 1)];

            var onRegionMission = CarrierWingsOnRegionMission(side);

            foreach (var group in groups[side])
            {
                if (group.Carrier is { } carrier)
                {
                    // 0xc66480: every wing on a carrier whose task force is in combat.
                    if (carrier.IsSunk)
                    {
                        continue;
                    }

                    // [LOG, unsettled] An escaped carrier's wings per rules.EscapedCarrierWings (A-DETECT-MIX).
                    var escaped = carrier.CurrentStatus == ShipStatus.Retreated;
                    var escapedRule = scenario.Rules.EscapedCarrierWings;

                    if (escaped && escapedRule == EscapedCarrierWings.Leave)
                    {
                        continue;
                    }

                    var scramble = !escaped || escapedRule == EscapedCarrierWings.Stay;

                    var effectiveness = CarrierEffectiveness(side, carrier, onRegionMission, conditions);

                    foreach (var wing in group.Wings.Where(wing => wing.Planes > 0))
                    {
                        var strike = wing.ExecutesNavalStrike;
                        var airSuperiority = !strike && wing.IsFighter;
                        // 0xf78360: trunc(round(planes × effectiveness) × CARRIER_OFFENSIVE_STANCE_SORTIE_RATIO[stance]).
                        var planes = (int)Math.Truncate(Math.Round(wing.Planes * effectiveness, MidpointRounding.AwayFromZero) * ratio);

                        if ((!strike && !airSuperiority) || planes <= 0)
                        {
                            continue;
                        }

                        if (scramble)
                        {
                            scrambled.Add(new RegionEntry(wing, group, true, strike, airSuperiority, planes));
                        }

                        if (onRegionMission)
                        {
                            regionList.Add(new RegionEntry(wing, group, true, strike, airSuperiority, planes));
                            wing.RegionMission = new RegionMission(true, MissionEfficiencyKey(strike, airSuperiority));
                        }
                    }
                }
                else if (group.LandBased is { } landBased)
                {
                    var effectiveness = LandEffectiveness(side, landBased, conditions);

                    foreach (var wing in group.Wings.Where(wing => wing.Planes > 0))
                    {
                        var planes = (int)Math.Round(wing.Planes * effectiveness, MidpointRounding.AwayFromZero);

                        if (planes > 0)
                        {
                            regionList.Add(new RegionEntry(wing, group, false, landBased.JoinsNavalBattle, !landBased.JoinsNavalBattle, planes));
                            wing.RegionMission = new RegionMission(false, MissionEfficiencyKey(landBased.JoinsNavalBattle, !landBased.JoinsNavalBattle));
                        }
                    }
                }
            }

            lists[side] = (regionList, scrambled);

            // [RE] A-DETECT 0xf77730: only the side's own region-list missions count (scrambled-only carrier wings do
            // not); a carrier wing is home in its task force's region, a land wing over a sea region never is.
            var country = side.Participant.CountryModifiers;
            var missions = regionList
                .Select(entry => new RegionAirWarRules.DetectionMission(entry.Planes, entry.AirSuperiority, false, entry.Carrier,
                    RegionAirWarRules.NightFactor(entry.Wing, country, defines), RegionAirWarRules.WeatherFactor(entry.Wing, country, defines)))
                .ToList();
            detection[side] = RegionAirWarRules.Detection(missions, country, side.Participant.RadarCoverage, 0, conditions.Darkness,
                conditions.WeatherModifier(StaticModifiers.AirDetection), defines);
        }

        foreach (var (own, enemy) in new[] { (Attacker, Defender), (Defender, Attacker) })
        {
            FightRegion(own, lists[own], lists[enemy], random);
        }

        // 0xf77360 / 0xc4c570: escorts of the side's air superiority missions cancel its disruption; only its region
        // missions keep what is left (scrambled-only wings are never disrupted).
        foreach (var side in new[] { Attacker, Defender })
        {
            var (regionList, scrambled) = lists[side];
            var wings = regionList.Concat(scrambled).GroupBy(entry => entry.Wing).Select(entries => entries.First()).ToList();
            var taken = wings.Sum(entry => entry.Wing.DisruptionTaken);
            var escorts = wings.Where(entry => entry.AirSuperiority)
                .Sum(entry => RegionAirWarRules.Escort(entry.Planes, RegionAirWarRules.Stats(entry.Wing, defines, side.Participant.CountryModifiers),
                    defines, side.Participant.CountryModifiers, detection[side]));

            if (taken <= 0)
            {
                continue;
            }

            var left = 1 - Math.Min(1, escorts / taken);

            foreach (var entry in regionList.Where(entry => entry.Strike).DistinctBy(entry => entry.Wing))
            {
                entry.Wing.Disruption = FixedPoint.Truncate(left * entry.Wing.DisruptionTaken);
            }
        }

        if (!applyLosses)
        {
            return;
        }

        // 0xf87550: the losses come off the wings after the whole air war.
        foreach (var side in new[] { Attacker, Defender })
        {
            foreach (var group in groups[side])
            {
                foreach (var wing in group.Wings.Where(wing => wing.RegionLosses > 0).ToList())
                {
                    var removed = Math.Min(wing.Planes, wing.RegionLosses);
                    wing.Planes -= removed;
                    Log.Add($"Hour {hour}: {side.Label} {group.Source} lost {removed} {wing.Plane.ID} in the region's air war");

                    // [LOG] A carrier wing emptied in the region's air war is deleted from its pool.
                    if (wing.Planes == 0 && group.Carrier is not null)
                    {
                        group.Wings.Remove(wing);
                    }
                }
            }
        }
    }

    /// <summary>
    /// A-REGION-SYNC [RE + LOG]: whether the side's carrier wings are on the region's mission list: the scenario's
    /// setting, else the game's case for a battle-start save (the attacker's naval mission tick gave its wings the region).
    /// </summary>
    public bool CarrierWingsOnRegionMission(BattleSide side)
    {
        return side.Participant.CarrierWingsOnRegionMission ?? ReferenceEquals(side, Attacker);
    }

    // [RE] A-REGION-SYNC 0xf78500: the effectiveness of a carrier's wings: the side's supply input × the deck crowding of
    // every plane on the carrier × (1 + air_mission_efficiency of the country, the fleet's admiral and, for wings with a
    // region mission, the region's weather).
    private double CarrierEffectiveness(BattleSide side, Ship carrier, bool onRegionMission, RegionAirConditions conditions)
    {
        var planes = groups[side].Where(group => ReferenceEquals(group.Carrier, carrier)).Sum(group => group.Planes);
        var modifier = side.Participant.CountryModifiers.Get(StaticModifiers.AirMissionEfficiency) +
                       (side.Participant.Admiral?.CombatModifiers.Get(StaticModifiers.AirMissionEfficiency) ?? 0) +
                       (onRegionMission ? conditions.WeatherModifier(StaticModifiers.AirMissionEfficiency) : 0);
        return RegionAirWarRules.Effectiveness(side.Participant.CarrierMissionEfficiency, AirRules.CrowdFactor(carrier, planes, defines), modifier);
    }

    // [RE] A-REGION-SYNC 0xf78500: a land wing's effectiveness: its input × (1 + air_mission_efficiency of the country and
    // the region's weather), at most 1.
    private static double LandEffectiveness(BattleSide side, LandBasedAirwing landBased, RegionAirConditions conditions)
    {
        var modifier = side.Participant.CountryModifiers.Get(StaticModifiers.AirMissionEfficiency) +
                       conditions.WeatherModifier(StaticModifiers.AirMissionEfficiency);
        return RegionAirWarRules.Effectiveness(landBased.MissionEfficiency, 1, modifier);
    }

    // [RE] A-STATS-MISSION-EFF 0xf7ed40: the mission efficiency of the executing mission (naval strike, air superiority).
    private static string? MissionEfficiencyKey(bool strike, bool airSuperiority)
    {
        return strike ? StaticModifiers.AirNavEfficiency : airSuperiority ? StaticModifiers.AirSuperiorityEfficiency : null;
    }

    // [RE] 0xc5f4c0 → 0xf81a20 → 0xf7be70 → 0xf862a0: one side's turn as the attacking country.
    private void FightRegion(BattleSide own, (List<RegionEntry> RegionList, List<RegionEntry> Scrambled) ownLists,
        (List<RegionEntry> RegionList, List<RegionEntry> Scrambled) enemyLists, IRandomSource random)
    {
        var enemy = ReferenceEquals(own, Attacker) ? Defender : Attacker;
        var defenders = enemyLists.RegionList.Concat(enemyLists.Scrambled).ToList();
        var attackers = ownLists.RegionList.Concat(ownLists.Scrambled.Where(entry => entry.AirSuperiority)).ToList();

        if (defenders.Count == 0 || attackers.Count == 0)
        {
            return;
        }

        var ownCountry = own.Participant.CountryModifiers;
        var enemyCountry = enemy.Participant.CountryModifiers;
        var ownDetection = detection[own];
        var anyStrike = defenders.Any(entry => entry.Strike);

        // 0xf80540: the defenders' weights; a carrier wing in combat counts fully, a land wing by the attacker's detection
        // (A-DETECT-USE: planes × detection in fixed point, then rounded).
        foreach (var defender in defenders)
        {
            var seen = FixedPoint.Truncate(defender.Planes * (defender.Carrier ? 1 : ownDetection));
            defender.Left = defender.AirSuperiority && anyStrike
                ? defender.Planes
                : Math.Max(1, (int)Math.Round(seen, MidpointRounding.AwayFromZero));
        }

        // A-STATS-GETTERS: the sort reads the stats without the province (no mission efficiency, no home defence), the
        // fight with it.
        var sortStats = new Dictionary<AirWing, RegionAirWarRules.WingStats>();
        var stats = new Dictionary<AirWing, RegionAirWarRules.WingStats>();
        RegionAirWarRules.WingStats SortStatsOf(RegionEntry entry) =>
            sortStats.TryGetValue(entry.Wing, out var known) ? known : sortStats[entry.Wing] = RegionAirWarRules.Stats(entry.Wing, defines, ownCountry);
        RegionAirWarRules.WingStats StatsOf(RegionEntry entry, ModifierSet country) =>
            stats.TryGetValue(entry.Wing, out var known) ? known : stats[entry.Wing] = RegionAirWarRules.Stats(entry.Wing, defines, country, inRegion: true);

        foreach (var attacker in attackers.OrderByDescending(entry => SortStatsOf(entry).AttackShare * SortStatsOf(entry).AgilityShare).ToList())
        {
            var s = StatsOf(attacker, ownCountry);

            if (s.Attack <= 0 || attacker.Planes <= 0)
            {
                continue;
            }

            var total = defenders.Sum(entry => entry.Left);

            if (total <= 0)
            {
                return;
            }

            var sameStrike = defenders.Where(entry => entry.Strike).Sum(entry => entry.Left);
            var engaged = Math.Min(total * defines.NAir.COMBAT_MULTIPLANE_CAP, attacker.Planes);
            var potential = engaged * s.Attack;

            foreach (var defender in defenders.Where(entry => entry.Left > 0))
            {
                // A-DETECT-USE [RE] 0xf7c282: the disruption share takes the defender's capacity before this attacker uses it.
                var left = defender.Left;
                var share = (double)left / total;
                var bothCarriers = defender.Carrier && attacker.Carrier;
                var d = StatsOf(defender, enemyCountry);
                var expected = RegionAirWarRules.ExpectedLosses(Math.Max(0.001, share * potential), s, d, bothCarriers, defines);
                var cap = Math.Min(defender.Left, defender.Planes - defender.Wing.RegionLosses);
                defender.Left = Math.Max(0, defender.Left - (int)Math.Round(Math.Max(0.001, share * engaged) / defines.NAir.COMBAT_MULTIPLANE_CAP,
                    MidpointRounding.AwayFromZero));
                defender.Wing.RegionLosses += RegionAirWarRules.Losses(expected, cap, random);

                if (defender.Strike && sameStrike > 0)
                {
                    var disruption = RegionAirWarRules.Disruption(attacker.Planes, s, bothCarriers, defines, ownDetection);
                    defender.Wing.DisruptionTaken += FixedPoint.Truncate(FixedPoint.Truncate((double)left / sameStrike * disruption));
                }
            }
        }
    }

    /// <summary>
    /// [RE] A-REINFORCE (0xd10310, 0xc5c0a0): the day change refills air wings from the side's stockpile, all missing
    /// planes at once, the wing missing most first (equal priority and mission for all). Carrier wings get nothing while
    /// their task force is in combat, so only land-based wings refill. A wing takes its own plane first, then [ASSUMED]
    /// other planes of its sub-unit with the same missions, counted as its own (the game mixes variants in a wing).
    /// Manpower is not checked. Returns a log line per refilled wing.
    /// </summary>
    public List<string> ReinforceDaily(BattleSide side)
    {
        var lines = new List<string>();
        var stock = stockpiles[side];
        var wings = groups[side]
            .Where(group => group.IsExternal)
            .SelectMany(group => group.Wings.Select(wing => (group, wing)))
            .Where(entry => entry.wing.Size > entry.wing.Planes)
            .OrderByDescending(entry => entry.wing.Size - entry.wing.Planes)
            .ToList();

        foreach (var (group, wing) in wings)
        {
            var added = 0;

            foreach (var planeId in StockCandidates(wing.Plane, stock))
            {
                var take = Math.Min(stock[planeId], wing.Size - wing.Planes);
                stock[planeId] -= take;
                wing.Planes += take;
                added += take;
            }

            if (added > 0)
            {
                lines.Add($"{side.Label} {group.Source} refilled {added} planes from the stockpile");
            }
        }

        return lines;
    }

    private IEnumerable<string> StockCandidates(PlaneEquipment plane, Dictionary<string, int> stock)
    {
        var same = stock.Keys.Where(id => id == plane.ID);
        var others = stock.Keys.Where(id => id != plane.ID && scenario.PlanesByID.TryGetValue(id, out var other) &&
                                            other.Pool == plane.Pool && other.Missions.SetEquals(plane.Missions));
        return same.Concat(others).Where(id => stock[id] > 0).ToList();
    }

    private BattleSide Defender => groups.Keys.First(side => !ReferenceEquals(side, Attacker));
}
