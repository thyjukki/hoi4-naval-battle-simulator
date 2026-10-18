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

    public AirPhase(BattleScenario scenario, BattleSide attacker, BattleSide defender)
    {
        this.scenario = scenario;
        defines = scenario.Defines;
        Attacker = attacker;

        foreach (var side in new[] { attacker, defender })
        {
            groups[side] = BuildGroups(side);
            externalJoined[side] = 0;
        }
    }

    public BattleSide Attacker { get; }
    public List<string> Notes { get; } = [];

    public IReadOnlyList<AirGroup> GroupsOf(BattleSide side)
    {
        return groups[side];
    }

    public bool HasAir => groups.Values.Any(list => list.Count > 0);

    private List<AirGroup> BuildGroups(BattleSide side)
    {
        var result = new List<AirGroup>();
        var assignments = side.Participant.Fleet.CarrierAirwingsByShipDesign;

        foreach (var carrier in side.Ships.Where(ship => ship.Role == ShipRole.Carrier))
        {
            if (!assignments.TryGetValue(carrier.Design.ID, out var designAssignments))
            {
                continue;
            }

            var wings = new List<AirWing>();

            foreach (var assignment in designAssignments)
            {
                if (!scenario.PlanesByID.TryGetValue(assignment.PlaneID, out var plane))
                {
                    Notes.Add($"Unknown plane '{assignment.PlaneID}' on {carrier.ID}; wing ignored.");
                    continue;
                }

                for (var i = 0; i < assignment.Airwings; i++)
                {
                    wings.Add(new AirWing(plane, assignment.Type, assignment.PlaneCount));
                }
            }

            if (wings.Count > 0)
            {
                result.Add(AirGroup.ForCarrier(carrier, wings));
            }
        }

        var participant = side.Participant;

        if (participant.ExternalNavalStrikePlanes > 0)
        {
            if (participant.ExternalPlaneId is { } planeId && scenario.PlanesByID.TryGetValue(planeId, out var plane))
            {
                result.Add(AirGroup.External([new AirWing(plane, AirwingType.Bomber, 0)], participant.ExternalMissionEfficiency));
            }
            else
            {
                Notes.Add($"{side.Label}: {participant.ExternalNavalStrikePlanes} land-based planes ignored (set externalPlaneID to a plane type).");
            }
        }

        return result;
    }

    public double SortieEfficiency(AirGroup group, BattleSide side, int hour, double darkness)
    {
        // [RE] The carrier's own sortie_efficiency modifiers (spirits, doctrines) add to the base.
        var carrier = group.Carrier!;
        var bonus = side.Participant.Spirits
            .Where(spirit => spirit.AppliesTo(carrier.Role, carrier.Design.Hull.Types))
            .Sum(spirit => spirit.SortieEffiency);
        return AirRules.SortieEfficiency(group, side.Screening, ReferenceEquals(side, Attacker), StanceIndex(side), hour, darkness, defines, bonus,
            side.Participant.CarrierNightTraffic);
    }

    private int StanceIndex(BattleSide side)
    {
        return side.Participant.CarrierStanceIndex ?? (int)defines.NNavy.CARRIER_OFFENSIVE_STANCE_DEFAULT_INDEX;
    }

    /// <summary>
    /// [RE] S-POS carrier-ratio inputs (0x161eeb0): A = sum of sortie efficiency * planes over carrier groups,
    /// B = mean of sortie efficiency * min(1, planes / CARRIER_PLANES_AMOUNT_FOR_POSITIONING).
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
            b += efficiency * Math.Min(1.0, group.Planes / defines.NAir.CARRIER_PLANES_AMOUNT_FOR_POSITIONING);
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

        JoinExternalPlanes(own, enemy, hour);
        var ownCarrierPlanes = ownGroups.Where(group => !group.IsExternal).Sum(group => group.Planes);
        var stacking = AirRules.StackingFactor(ownCarrierPlanes, AirRules.PlaneCap(enemy.Ships, defines), defines);

        foreach (var group in ownGroups)
        {
            var strikeWings = group.StrikeWings.ToList();

            if (strikeWings.Count == 0 || group.Carrier is { IsInBattle: false })
            {
                continue;
            }

            // [RE] Only the first round(n * F) strike wings feed the averages.
            var selected = strikeWings.Take(Math.Max(1, (int)Math.Round(strikeWings.Count * stacking, MidpointRounding.AwayFromZero))).ToList();
            var (attack, targeting, agility) = AirRules.StrikeAverages(selected, defines);
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

            var source = group.Carrier?.ID ?? "land-based";
            var defenders = groups[enemy]
                .Where(defending => defending.Carrier is { IsInBattle: true })
                .SelectMany(defending => defending.FighterWings)
                .ToList();
            var ratios = defines.NNavy.CARRIER_OFFENSIVE_STANCE_SORTIE_RATIO;
            var defendingShare = 1.0 - (ratios.Length == 0 ? 0.5 : ratios[Math.Clamp(StanceIndex(enemy), 0, ratios.Length - 1)]);
            var intercepted = Math.Min(flying, AirRules.Interception(strikeWings, defenders, defendingShare, defines, random));
            AirRules.RemoveLosses(strikeWings, intercepted);

            var targetAntiAir = target.Role == ShipRole.Convoy ? 0 : target.GetFinalStats().AntiAir;
            var inReach = (int)Math.Ceiling(Math.Max(0, flying - intercepted) * stacking);
            var shotDown = Math.Min(flying - intercepted, AirRules.PreemptiveAntiAir(targetAntiAir, agility, inReach, defines, random));
            AirRules.RemoveLosses(group.StrikeWings.Concat(group.FighterWings), shotDown);

            var strikers = flying - intercepted - shotDown;
            var hits = strikers > 0 ? AirRules.Hits(strikers, targeting, defines, random) : 0;
            var reduction = AirRules.AntiAirReduction(targetAntiAir, AirRules.FleetAntiAir(enemy.Ships, enemy.Positioning, defines), defines);

            if (hits <= 0)
            {
                results.Add((new AirStrikeRecord(hour, own.Label, source, target.ID, flying, intercepted, shotDown, 0, reduction, false, 0, 0, false),
                    target, default));
                continue;
            }

            var perPlaneAttack = attack * (group.IsExternal ? group.MissionEfficiency : defines.NAir.NAVAL_STRIKE_CARRIER_MULTIPLIER);
            var damage = hits * perPlaneAttack * (1.0 - reduction);
            var strDamage = damage * defines.NAir.NAVAL_STRIKE_DAMAGE_TO_STR;
            var orgDamage = damage * defines.NAir.NAVAL_STRIKE_DAMAGE_TO_ORG;
            var critical = RollCritical(target, hits, ref strDamage, ref orgDamage, random);
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
    private bool RollCritical(Ship target, int hits, ref double strDamage, ref double orgDamage, IRandomSource random)
    {
        var chance = AirRules.CriticalChance(target, defines);

        if (chance <= 0 || random.NextDouble() >= chance)
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
                random.NextDouble();
            }

            strDamage *= multiplier;
            orgDamage *= multiplier;
        }

        return true;
    }

    // [RE] A-EXTERNAL: land-based planes join up to the cumulative cap; lost planes do not free room.
    private void JoinExternalPlanes(BattleSide own, BattleSide enemy, int hour)
    {
        var external = groups[own].FirstOrDefault(group => group.IsExternal);

        if (external is null)
        {
            return;
        }

        var cap = (int)AirRules.ExternalJoinCap(enemy.Ships, hour, defines);
        var offered = own.Participant.ExternalNavalStrikePlanes;
        var add = Math.Max(0, Math.Min(offered, cap) - externalJoined[own]);

        if (add > 0)
        {
            external.Wings[0].Planes += add;
            externalJoined[own] += add;
        }
    }
}
