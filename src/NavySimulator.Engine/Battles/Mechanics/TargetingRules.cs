using NavySimulator.Engine.Defines;
using NavySimulator.Engine.Random;

namespace NavySimulator.Engine.Battles.Mechanics;

/// <summary>
/// S-TARGET: which enemy groups a weapon can reach and which ship it picks. Spec: docs/mechanics/surface.md.
/// Group membership is evaluated live, so a group wiped out earlier in the hour no longer blocks the next one.
/// </summary>
public static class TargetingRules
{
    private static readonly GroupType[] SurfaceGroupOrder = [GroupType.Screen, GroupType.Capital, GroupType.Carrier];

    public static List<GroupType> ReachableGroups(
        Ship shooter,
        WeaponType weapon,
        BattleSide enemy,
        SubmarineEngagementRisk submarineRisk,
        IRandomSource random)
    {
        switch (weapon)
        {
            case WeaponType.Light:
            {
                // [WIKI] Closest non-empty group; carrier-group shooters never target the enemy carrier group.
                var excludeCarrierGroup = shooter.Role is ShipRole.Carrier or ShipRole.Convoy;
                var closest = SurfaceGroupOrder
                    .Where(group => !(excludeCarrierGroup && group == GroupType.Carrier))
                    .FirstOrDefault(group => HasShips(enemy, group), (GroupType)(-1));
                return (int)closest < 0 ? [] : [closest];
            }
            case WeaponType.Heavy:
                // [WIKI] First two non-empty groups.
                return SurfaceGroupOrder.Where(group => HasShips(enemy, group)).Take(2).ToList();
            case WeaponType.Torpedo:
                return shooter.Role == ShipRole.Submarine
                    ? SubmarineTorpedoGroups(enemy, submarineRisk, random)
                    : SurfaceTorpedoGroups(enemy, random);
            case WeaponType.DepthCharge:
                // [ASSUMED] Every submarine in battle is targetable; hide/reveal is not modelled yet (S-SUB).
                return HasShips(enemy, GroupType.Submarine) ? [GroupType.Submarine] : [];
            default:
                return [];
        }
    }

    // [WIKI] Screens are always reachable; capitals with chance (1 - screening efficiency);
    // the carrier group additionally needs (1 - carrier screening efficiency).
    private static List<GroupType> SurfaceTorpedoGroups(BattleSide enemy, IRandomSource random)
    {
        var groups = new List<GroupType>();

        if (HasShips(enemy, GroupType.Screen))
        {
            groups.Add(GroupType.Screen);
        }

        var bypassesScreens = random.NextDouble() < 1.0 - enemy.Screening.ScreeningEfficiency;
        var bypassesCapitals = bypassesScreens && random.NextDouble() < 1.0 - enemy.Screening.CarrierScreeningEfficiency;

        if (bypassesScreens && HasShips(enemy, GroupType.Capital))
        {
            groups.Add(GroupType.Capital);
        }

        if (bypassesCapitals && HasShips(enemy, GroupType.Carrier))
        {
            groups.Add(GroupType.Carrier);
        }

        if (groups.Count == 0)
        {
            var closest = SurfaceGroupOrder.FirstOrDefault(group => HasShips(enemy, group), (GroupType)(-1));
            return (int)closest < 0 ? [] : [closest];
        }

        return groups;
    }

    // [WIKI + ASSUMED] Subs attack screens only on High risk and capitals on Medium or higher; the screening
    // bypass rolls are assumed to apply to submarine torpedoes as well.
    private static List<GroupType> SubmarineTorpedoGroups(BattleSide enemy, SubmarineEngagementRisk risk, IRandomSource random)
    {
        var groups = new List<GroupType>();

        if (risk == SubmarineEngagementRisk.High && HasShips(enemy, GroupType.Screen))
        {
            groups.Add(GroupType.Screen);
        }

        var bypassesScreens = random.NextDouble() < 1.0 - enemy.Screening.ScreeningEfficiency;
        var bypassesCapitals = bypassesScreens && random.NextDouble() < 1.0 - enemy.Screening.CarrierScreeningEfficiency;

        if (risk >= SubmarineEngagementRisk.Medium && bypassesScreens && HasShips(enemy, GroupType.Capital))
        {
            groups.Add(GroupType.Capital);
        }

        if (bypassesCapitals && HasShips(enemy, GroupType.Carrier))
        {
            groups.Add(GroupType.Carrier);
        }

        return groups;
    }

    public static Ship? SelectTarget(
        Ship shooter,
        WeaponType weapon,
        IReadOnlyList<GroupType> groups,
        BattleSide enemy,
        Func<Ship, bool> isActivelyFighting,
        GameDefines defines,
        IRandomSource random)
    {
        var candidates = groups
            .SelectMany(group => ShipsInGroup(enemy, group))
            .Select(target => (Target: target, Weight: TargetWeight(shooter, weapon, target, isActivelyFighting(target), defines)))
            .Where(candidate => candidate.Weight > 0)
            .ToList();

        if (candidates.Count == 0)
        {
            return null;
        }

        // [WIKI] A retreating shooter only considers actively fighting targets and always takes the highest score.
        if (shooter.CurrentStatus == ShipStatus.Retreating)
        {
            var fighting = candidates.Where(candidate => isActivelyFighting(candidate.Target)).ToList();
            var pool = fighting.Count > 0 ? fighting : candidates;
            return pool
                .OrderByDescending(candidate => candidate.Weight)
                .ThenBy(candidate => candidate.Target.ID, StringComparer.Ordinal)
                .First()
                .Target;
        }

        var totalWeight = candidates.Sum(candidate => candidate.Weight);
        var roll = random.NextDouble() * totalWeight;
        var cumulative = 0.0;

        foreach (var candidate in candidates)
        {
            cumulative += candidate.Weight;

            if (roll < cumulative)
            {
                return candidate.Target;
            }
        }

        return candidates[^1].Target;
    }

    // [WIKI] base weight by group and weapon, x(1 + damaged fraction), x0.5 if retreating, +50% if actively fighting.
    public static double TargetWeight(Ship shooter, WeaponType weapon, Ship target, bool targetActivelyFighting, GameDefines defines)
    {
        var hardcoded = defines.Hardcoded;
        var light = weapon is WeaponType.Light;
        var baseWeight = target.Role switch
        {
            ShipRole.Capital => light ? hardcoded.TargetWeightCapitalLight : hardcoded.TargetWeightCapital,
            ShipRole.Screen => light ? hardcoded.TargetWeightScreenLight : hardcoded.TargetWeightScreen,
            ShipRole.Carrier => light ? hardcoded.TargetWeightCarrierLight : hardcoded.TargetWeightCarrier,
            ShipRole.Convoy when shooter.Role == ShipRole.Submarine =>
                light ? hardcoded.TargetWeightConvoyFromSubmarineLight : hardcoded.TargetWeightConvoyFromSubmarine,
            ShipRole.Convoy => light ? hardcoded.TargetWeightConvoyLight : hardcoded.TargetWeightConvoy,
            ShipRole.Submarine => hardcoded.TargetWeightSubmarine,
            _ => 1.0
        };

        var maxHp = target.GetFinalStats().Hp;
        var damagedFraction = maxHp <= 0 ? 0 : Math.Clamp(1.0 - target.CurrentHP / maxHp, 0, 1);
        var weight = baseWeight * (1.0 + hardcoded.TargetWeightMaxDamagedBonus * damagedFraction);

        if (target.CurrentStatus == ShipStatus.Retreating)
        {
            weight *= hardcoded.TargetWeightRetreatingMultiplier;
        }

        if (targetActivelyFighting)
        {
            weight *= 1.0 + hardcoded.TargetWeightActivelyFightingBonus;
        }

        return weight;
    }

    public static IEnumerable<Ship> ShipsInGroup(BattleSide side, GroupType group)
    {
        var lines = side.Lines;
        IEnumerable<Ship> ships = group switch
        {
            GroupType.Screen => lines.Screens,
            GroupType.Capital => lines.Capitals,
            GroupType.Carrier => lines.Carriers.Concat(lines.Convoys),
            GroupType.Submarine => lines.Submarines,
            _ => []
        };

        return ships.Where(ship => ship.IsInBattle);
    }

    private static bool HasShips(BattleSide side, GroupType group)
    {
        return ShipsInGroup(side, group).Any();
    }
}
