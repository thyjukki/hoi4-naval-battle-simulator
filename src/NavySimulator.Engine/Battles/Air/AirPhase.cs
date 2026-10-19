using NavySimulator.Engine.Battles.Mechanics;
using NavySimulator.Engine.Defines;
using NavySimulator.Engine.Random;
using NavySimulator.Engine.Results;

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
                    wings.Add(new AirWing(plane, assignment.PlaneCount, defines.Hardcoded, executes));
                }
            }

            // [RE] A-WINGS-STRIKE (0x1c6a7c0): one air object per wing pool (plane sub-unit) on the carrier, in order of first wing.
            foreach (var pool in wings.GroupBy(wing => wing.Plane.Pool))
            {
                result.Add(AirGroup.ForCarrier(carrier, pool.ToList(), pool.Key));
            }
        }

        // A-EXTERNAL: one air object per land-based entry (the game's per air base AirFX); it strikes once it has joined.
        foreach (var landBased in side.Participant.LandBasedAirwings.Where(entry => entry.Planes > 0))
        {
            if (scenario.PlanesByID.TryGetValue(landBased.PlaneID, out var plane))
            {
                result.Add(AirGroup.External([new AirWing(plane, landBased.Planes, defines.Hardcoded)], landBased.DisruptionFactor, landBased));
            }
            else
            {
                Notes.Add($"{side.Label}: {landBased.Planes} land-based planes of unknown type '{landBased.PlaneID}' ignored.");
            }
        }

        return result;
    }

    public double SortieEfficiency(AirGroup group, BattleSide side, int hour, double darkness)
    {
        return AirRules.SortieEfficiency(group, side.Screening, ReferenceEquals(side, Attacker), StanceIndex(side), hour, darkness, defines,
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
    public (double A, double B) CarrierAirStrength(BattleSide side, int hour, double darkness)
    {
        var carrierGroups = groups[side].Where(group => group.Carrier is { IsInBattle: true }).ToList();

        if (carrierGroups.Count == 0)
        {
            return (0, 0);
        }

        var a = 0.0;
        var b = 0.0;

        foreach (var group in carrierGroups)
        {
            var efficiency = SortieEfficiency(group, side, hour, darkness);
            a += efficiency * group.Planes;
            b += efficiency * Math.Min(1.0, CarrierPlanes(group.Carrier!) / defines.NAir.CARRIER_PLANES_AMOUNT_FOR_POSITIONING);
        }

        return (a, b / carrierGroups.Count);
    }

    public List<(AirStrikeRecord Record, Ship Target, AppliedDamage Applied)> Strike(
        BattleSide own,
        BattleSide enemy,
        int hour,
        double darkness,
        IRandomSource random)
    {
        var results = new List<(AirStrikeRecord, Ship, AppliedDamage)>();
        var ownGroups = groups[own];

        if (ownGroups.Count == 0)
        {
            return results;
        }

        var ownCarrierPlanes = ownGroups.Where(group => !group.IsExternal).Sum(group => group.Planes);
        var stacking = AirRules.StackingFactor(ownCarrierPlanes, AirRules.PlaneCap(enemy.Ships, defines), defines);

        foreach (var group in ownGroups)
        {
            var strikeWings = group.StrikeWings.ToList();

            if (strikeWings.Count == 0 || !group.HasJoined || group.Carrier is { IsInBattle: false })
            {
                continue;
            }

            // [RE] Only the first round(n * F) strike wings, of those executing naval strike, feed the averages (0x1978e30).
            var selected = strikeWings.Take(Math.Max(1, (int)Math.Round(strikeWings.Count * stacking, MidpointRounding.AwayFromZero)))
                .Where(wing => wing.ExecutesNavalStrike)
                .ToList();
            var (attack, targeting, agility) = AirRules.StrikeAverages(selected, defines, own.Participant.CountryModifiers, !group.IsExternal);
            var flying = group.IsExternal
                ? group.Planes
                : (int)Math.Round(group.Planes * Math.Clamp(SortieEfficiency(group, own, hour, darkness), 0, 1), MidpointRounding.AwayFromZero);

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
            var defenders = groups[enemy]
                .Where(defending => defending.Carrier is { IsInBattle: true })
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

            var strikers = flying - intercepted - shotDown;
            var hits = strikers > 0 ? AirRules.Hits(strikers, targeting, defines, random) : 0;
            var reduction = AirRules.AntiAirReduction(targetAntiAir, AirRules.FleetAntiAir(enemy.Ships, enemy.Positioning, defines), defines);

            if (hits <= 0)
            {
                results.Add((new AirStrikeRecord(hour, own.Label, source, target.ID, flying, intercepted, shotDown, 0, reduction, false, 0, 0, false),
                    target, default));
                continue;
            }

            var perPlaneAttack = attack * (group.IsExternal ? group.DisruptionFactor : defines.NAir.NAVAL_STRIKE_CARRIER_MULTIPLIER);
            var damage = hits * perPlaneAttack * (1.0 - reduction);
            var strDamage = damage * defines.NAir.NAVAL_STRIKE_DAMAGE_TO_STR;
            var orgDamage = damage * defines.NAir.NAVAL_STRIKE_DAMAGE_TO_ORG;
            // [RE] 0x197af20: the target's critical_receive_chance through the member getter (its side's screening bonuses).
            var receive = MemberModifierRules.Get(target, StaticModifiers.CriticalReceiveChance, enemy.Screening, darkness, defines);
            var critical = RollCritical(target, hits, receive, ref strDamage, ref orgDamage, random);
            var applied = DamageRules.Apply(target, strDamage, orgDamage, defines);

            results.Add((new AirStrikeRecord(hour, own.Label, source, target.ID, flying, intercepted, shotDown, hits, reduction, critical,
                applied.HpDamage, applied.OrganizationDamage, applied.Sunk), target, applied));
        }

        return results;
    }

    private Ship? PickTarget(BattleSide enemy, IRandomSource random)
    {
        var candidates = enemy.Ships
            .Where(ship => ship.IsInBattle && ship.IsVisible)
            .Select(ship => (ship, AirRules.TargetWeight(ship, enemy.Screening.ScreeningEfficiency, defines)))
            .Where(candidate => candidate.Item2 > 0)
            .ToList();
        return TargetingRules.WeightedPick(candidates, random);
    }

    // [RE] A-CRIT; a part hit would replace the multiplier, but no part data is loaded (the "no candidates" path).
    private bool RollCritical(Ship target, int hits, double criticalReceiveChance, ref double strDamage, ref double orgDamage, IRandomSource random)
    {
        var chance = AirRules.CriticalChance(target, criticalReceiveChance, defines);

        if (chance <= 0 || random.NextDouble(RollKind.Air) >= chance)
        {
            return false;
        }

        var multiplier = AirRules.CriticalMultiplier(target, hits, defines);

        if (multiplier > 1)
        {
            var reliability = Math.Min(target.GetFinalStats().Reliability, 1);
            var partChance = reliability > 0 ? defines.NNavy.CHANCE_TO_DAMAGE_PART_ON_CRITICAL_HIT_FROM_AIR / reliability : 1;

            if (partChance < 1 && reliability > 0 && target.Role != ShipRole.Convoy)
            {
                random.NextDouble(RollKind.Air);
            }

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
    public void JoinLandBased(int hour)
    {
        foreach (var (own, enemy) in new[] { (Attacker, Defender), (Defender, Attacker) })
        {
            externalJoined[own] = 0;
            var cap = (int)AirRules.ExternalJoinCap(enemy.Ships, hour, defines);

            foreach (var external in groups[own].Where(group => group.LandBased is not null))
            {
                if (cap <= 0 || externalJoined[own] >= cap)
                {
                    break;
                }

                var budget = (int)Math.Round(external.Planes * external.LandBased!.MissionEfficiency, MidpointRounding.AwayFromZero);
                var add = Math.Clamp(Math.Min(budget, cap - externalJoined[own]), 0, int.MaxValue);
                externalJoined[own] += add;
                external.Offered = Math.Min(add, external.Planes);
                external.HasJoined = true;
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
