using NavySimulator.Engine.Defines;
using NavySimulator.Engine.Random;

namespace NavySimulator.Engine.Battles.Mechanics;

/// <summary>
/// S-CRIT-PART [RE + CAL] (0x1974511, 0xc34500, 0xc31030): whether a critical hit damages a part, which one, and what
/// it does to the hit. Spec: docs/mechanics/surface.md.
/// </summary>
public static class CriticalPartRules
{
    /// <summary>
    /// [RE] 0x1974511 / 0x1975150: on a crit, a part is damaged with chance <c>CHANCE_TO_DAMAGE_PART_ON_CRITICAL_HIT ×
    /// critical_part_damage_chance_mult / reliability</c>; no roll (always) when that is at least 1 or reliability is 0.
    /// Air crits pass <c>CHANCE_TO_DAMAGE_PART_ON_CRITICAL_HIT_FROM_AIR</c> as <paramref name="baseChance"/> (A-CRIT).
    /// </summary>
    public static bool RollPartDamage(Ship target, GameDefines defines, IRandomSource random, double? baseChance = null)
    {
        if (target.Role == ShipRole.Convoy)
        {
            return false;
        }

        var reliability = Math.Min(target.GetFinalStats().Reliability, 1);
        var chance = (baseChance ?? defines.NNavy.CHANCE_TO_DAMAGE_PART_ON_CRITICAL_HIT) * target.Design.ShipType.CriticalPartDamageChanceMult;

        if (reliability <= 0)
        {
            return true;
        }

        chance /= reliability;
        return chance >= 1 || random.NextDouble(RollKind.PartDamage) < chance;
    }

    /// <summary>
    /// [RE] 0xc34500: picks the part among the target's candidates that are not damaged their maximum number of times,
    /// weighted by <c>chance</c> (<c>r = trunc(U × Σ chance)</c>, then the first candidate that takes <c>r -= chance</c>
    /// below 0), damages it and returns it; null when no candidate is left (the regular critical multiplier applies).
    /// [CAL] All 26 part picks of 18 logged TFB battles fit this order and the parts' damage multipliers.
    /// </summary>
    public static CriticalPart? DamageRandomPart(Ship target, GameDefines defines, IRandomSource random)
    {
        var candidates = target.Design.CriticalPartCandidates
            .Where(candidate => candidate.MaxInstances <= -1 || target.DamageCount(candidate.Part) < candidate.MaxInstances)
            .ToList();

        if (candidates.Count == 0)
        {
            return null;
        }

        // Weights in the game's fixed point, so the walk is the game's integer arithmetic.
        var weights = candidates.Select(candidate => (long)Math.Round(candidate.Part.Chance * 100000)).ToList();
        var remaining = (long)Math.Floor(random.NextDouble(RollKind.PartPick) * weights.Sum());
        var part = candidates[^1].Part;

        for (var i = 0; i < candidates.Count; i++)
        {
            remaining -= weights[i];

            if (remaining < 0)
            {
                part = candidates[i].Part;
                break;
            }
        }

        target.DamagePart(part);

        // [RE] 0xc3d4a3: the stat recompute clamps current organization to the new maximum.
        target.CurrentOrganization = Math.Min(target.CurrentOrganization, CombatStatRules.MaxOrganization(target, defines));
        return part;
    }
}
