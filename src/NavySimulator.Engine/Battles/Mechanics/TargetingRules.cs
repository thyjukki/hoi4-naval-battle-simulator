using NavySimulator.Engine.Defines;
using NavySimulator.Engine.Random;
using NavySimulator.Engine.Stats;

namespace NavySimulator.Engine.Battles.Mechanics;

/// <summary>
/// S-TARGET [RE]: which enemy ships a weapon slot can reach and which one it picks (0x1971cc0, 0x1972c00, 0x1971440).
/// Spec: docs/mechanics/surface.md. Line numbers follow the game's combat boxes: 1 screens, 2 capitals, 3 carriers
/// and convoys; submarines are outside the line range check.
/// </summary>
public static class TargetingRules
{
    public static int LineOf(Ship ship)
    {
        return ship.Role switch
        {
            ShipRole.Screen => 1,
            ShipRole.Capital => 2,
            ShipRole.Carrier or ShipRole.Convoy => 3,
            _ => 0
        };
    }

    /// <summary>
    /// [RE] Farthest line the slot reaches this time. Uses the enemy counts snapshot taken at the start of the side's
    /// turn. The torpedo slot rolls once against the enemy screening values, each first made
    /// <c>1 - (1 - screening) × (1 + penetration)</c> by the shooter's <c>naval_torpedo_screen_penetration_factor</c>.
    /// </summary>
    public static int MaxLine(WeaponSlot slot, EnemyLineCounts enemy, ScreeningSummary enemyScreening, IRandomSource random,
        double screenPenetration = 0)
    {
        switch (slot)
        {
            case WeaponSlot.Light:
                return enemy.Screens > 0 ? 1 : enemy.Capitals > 0 ? 2 : 3;
            case WeaponSlot.Heavy:
                return enemy.Capitals == 0 || enemy.Screens == 0 ? 3 : 2;
            default:
            {
                // [RE] 0x1972cb7: the penetration scales the unscreened share (S-TARGET-TORP).
                var screening = Penetrated(enemyScreening.ScreeningEfficiency, screenPenetration);
                var carrierScreening = Penetrated(enemyScreening.CarrierScreeningEfficiency, screenPenetration);
                var roll = random.NextDouble(RollKind.TorpedoLine);
                return roll <= screening ? 1
                    : roll <= carrierScreening ? 2
                    : 3;
            }
        }
    }

    private static double Penetrated(double screening, double penetration)
    {
        return penetration == 0 ? screening : Math.Max(0, 1 - Math.Max(0, 1 - screening) * (1 + penetration));
    }

    /// <summary>
    /// [RE] Nearest line a submarine may attack, from its task force aggressiveness. Higher lines fall back to lower
    /// ones only when they are empty. Surface ships use line 1.
    /// </summary>
    public static int MinLine(Ship shooter, Aggressiveness aggressiveness, EnemyLineCounts enemy)
    {
        if (shooter.Role != ShipRole.Submarine)
        {
            return 1;
        }

        var capitalsOrCarrierLine = enemy.Capitals + enemy.CarrierLine;
        return aggressiveness switch
        {
            <= Aggressiveness.EngageAtLowRisk => enemy.CarrierLine > 0 ? 3 : capitalsOrCarrierLine > 0 ? 2 : 1,
            Aggressiveness.EngageAtMediumRisk => capitalsOrCarrierLine > 0 ? 2 : 1,
            _ => 1
        };
    }

    /// <summary>Picks a target for the slot, or null when nothing is in reach (the slot then does not fire).</summary>
    public static Ship? SelectTarget(
        Ship shooter,
        WeaponSlot slot,
        BattleSide own,
        BattleSide enemy,
        GameDefines defines,
        IRandomSource random,
        double darkness = 0)
    {
        var stats = shooter.GetFinalStats();

        // [RE] A ship with only depth charges never picks a target.
        if (stats.LightAttack <= 0 && stats.HeavyAttack <= 0 && stats.TorpedoAttack <= 0)
        {
            return null;
        }

        // [RE] The penetration is the shooter's modifier 0x1bd through the member getter (convoys: 0).
        var penetration = slot == WeaponSlot.Torpedo && shooter.Role != ShipRole.Convoy
            ? MemberModifierRules.Get(shooter, StaticModifiers.NavalTorpedoScreenPenetrationFactor, own.Screening, darkness, defines)
            : 0;
        var maxLine = MaxLine(slot, own.EnemyCounts, enemy.Screening, random, penetration);
        var minLine = MinLine(shooter, own.Participant.Aggressiveness, own.EnemyCounts);
        var candidates = new List<(Ship Target, double Weight)>();

        foreach (var target in enemy.Ships)
        {
            if (!target.IsInBattle || !target.IsVisible)
            {
                continue;
            }

            if (target.Role == ShipRole.Submarine)
            {
                // Submarines are reached by the light slot (depth charges), or by a submarine's guns if retreating.
                var reachable = slot == WeaponSlot.Light ||
                                (shooter.Role == ShipRole.Submarine && target.CurrentStatus == ShipStatus.Retreating);

                if (!reachable)
                {
                    continue;
                }
            }
            else
            {
                var line = LineOf(target);

                if (line < minLine || line > maxLine)
                {
                    continue;
                }
            }

            var weight = TargetWeight(shooter, slot, target, defines);

            if (weight > 0)
            {
                candidates.Add((target, weight));
            }
        }

        return WeightedPick(candidates, random);
    }

    /// <summary>[RE] 0x1971440. Each step truncates to the game's fx5 fixed point, as the game does.</summary>
    public static double TargetWeight(Ship shooter, WeaponSlot slot, Ship target, GameDefines defines)
    {
        var hardcoded = defines.Hardcoded;
        var heavyOrTorpedo = slot is WeaponSlot.Heavy or WeaponSlot.Torpedo;
        double weight;

        if (target.Role == ShipRole.Convoy)
        {
            weight = shooter.Role == ShipRole.Submarine ? hardcoded.TargetBaseConvoyFromSubmarine : hardcoded.TargetBaseConvoy;

            if (heavyOrTorpedo)
            {
                weight *= hardcoded.TargetHeavyTorpedoMultiplier;
            }

            weight = FixedPoint.Truncate(weight * (1.0 + hardcoded.TargetConvoyDamagedFactor * (1.0 - FixedPointHpRatio(target))));
        }
        else
        {
            weight = target.Role switch
            {
                ShipRole.Capital => hardcoded.TargetBaseCapital,
                ShipRole.Screen => hardcoded.TargetBaseScreen,
                // [RE + ASSUMED stat] The game requires a shooter stat (ship+0x228) above zero; sub detection is assumed.
                ShipRole.Submarine => shooter.GetFinalStats().SubDetection > 0 ? hardcoded.TargetBaseSubmarine : 0,
                _ => hardcoded.TargetBaseOther
            };

            if (heavyOrTorpedo && target.Role is ShipRole.Capital or ShipRole.Carrier)
            {
                weight *= hardcoded.TargetHeavyTorpedoMultiplier;
            }

            if (slot == WeaponSlot.Light && target.Role == ShipRole.Screen)
            {
                weight *= hardcoded.TargetLightScreenMultiplier;
            }

            weight = FixedPoint.Truncate(weight * (2.0 - FixedPointHpRatio(target)));
        }

        // Exclusive state multiplier. Convoys are always retreating once their line is active.
        return target.CurrentStatus switch
        {
            ShipStatus.Active => FixedPoint.Truncate(weight * hardcoded.TargetFightingMultiplier),
            ShipStatus.Retreating => FixedPoint.Truncate(weight * hardcoded.TargetRetreatingMultiplier),
            _ => weight
        };
    }

    /// <summary>
    /// [RE] T = trunc(U * sum) in fx5; the heaviest candidate takes [0, maxW], the rest follow in list order. [CAL] In
    /// double arithmetic one pick of 11 replayed battles (about 120,000 shots) fell on the other side of a boundary.
    /// </summary>
    public static Ship? WeightedPick(IReadOnlyList<(Ship Target, double Weight)> candidates, IRandomSource random)
    {
        if (candidates.Count == 0)
        {
            return null;
        }

        var weights = candidates.Select(candidate => FixedPoint.Raw(candidate.Weight)).ToList();
        var total = weights.Sum();

        if (total <= 0)
        {
            return null;
        }

        var heaviest = 0;

        for (var i = 1; i < candidates.Count; i++)
        {
            if (weights[i] > weights[heaviest])
            {
                heaviest = i;
            }
        }

        var threshold = FixedPoint.Raw(random.NextDouble(RollKind.TargetPick) * total / FixedPoint.Scale);
        var cumulative = weights[heaviest];

        if (cumulative >= threshold)
        {
            return candidates[heaviest].Target;
        }

        for (var i = 0; i < candidates.Count; i++)
        {
            if (i == heaviest)
            {
                continue;
            }

            cumulative += weights[i];

            if (cumulative >= threshold)
            {
                return candidates[i].Target;
            }
        }

        return candidates[heaviest].Target;
    }

    public static double HpRatio(Ship ship)
    {
        return ship.CurrentHP / Math.Max(ship.GetFinalStats().Hp, 0.001);
    }

    // [RE + CAL] 0xc3ac60 in the game's fixed point, for the target weight.
    private static double FixedPointHpRatio(Ship ship)
    {
        return FixedPoint.Divide(ship.CurrentHP, Math.Max(ship.GetFinalStats().Hp, 0.001));
    }
}
