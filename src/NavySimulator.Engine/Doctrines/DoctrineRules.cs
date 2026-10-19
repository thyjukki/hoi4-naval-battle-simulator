using NavySimulator.Engine.Defines;

namespace NavySimulator.Engine.Doctrines;

/// <summary>
/// S-DOCTRINE: which parts of a side's doctrines are active, from the grand doctrine, the slotted subdoctrines and each
/// track's mastery. Spec: docs/mechanics/surface.md.
/// </summary>
public static class DoctrineRules
{
    /// <summary>
    /// S-DOCTRINE-MASTERY [RE] 0x9e4da0: a reward's own <c>mastery</c>, else (missing or 0) <c>NDoctrines.DEFAULT_REWARD_MASTERY</c>.
    /// </summary>
    public static double RewardMastery(DoctrineReward reward, GameDefines defines)
    {
        return reward.Mastery is { } mastery && mastery != 0 ? mastery : defines.NDoctrines.DEFAULT_REWARD_MASTERY;
    }

    /// <summary>
    /// S-DOCTRINE-MASTERY [RE] 0x147c000: the track mastery at which reward <paramref name="index"/> unlocks. Track mastery
    /// is cumulative and rewards unlock in order, so the thresholds are the summed costs.
    /// </summary>
    public static double Threshold(Subdoctrine subdoctrine, int index, GameDefines defines)
    {
        return subdoctrine.Rewards.Take(index + 1).Sum(reward => RewardMastery(reward, defines));
    }

    /// <summary>The mastery that unlocks every reward of the subdoctrine.</summary>
    public static double MaxMastery(Subdoctrine subdoctrine, GameDefines defines)
    {
        return subdoctrine.Rewards.Sum(reward => RewardMastery(reward, defines));
    }

    /// <summary>How many of the subdoctrine's rewards the mastery unlocks.</summary>
    public static int UnlockedRewards(Subdoctrine subdoctrine, double mastery, GameDefines defines)
    {
        var unlocked = 0;

        while (unlocked < subdoctrine.Rewards.Count && mastery >= Threshold(subdoctrine, unlocked, defines))
        {
            unlocked++;
        }

        return unlocked;
    }

    /// <summary>S-DOCTRINE-MASTERY [RE] 0xfc87b0: a track is complete, and gives its milestone, once all its rewards are unlocked.</summary>
    public static bool IsComplete(TrackSetup track, GameDefines defines)
    {
        return UnlockedRewards(track.Subdoctrine, track.Mastery, defines) == track.Subdoctrine.Rewards.Count;
    }

    /// <summary>
    /// S-DOCTRINE-EFFECT [RE] 0x13cdc50: the effects a setup gives, each with its source: the grand doctrine; per slotted
    /// track (in the grand doctrine's track order) the subdoctrine, its unlocked rewards and, when the track is complete,
    /// the grand doctrine's milestone for it.
    /// </summary>
    public static IEnumerable<(string Source, CountryEffects Effects)> ActiveEffects(DoctrineSetup setup, GameDefines defines)
    {
        var grand = setup.GrandDoctrine;
        yield return (grand.ID, grand.Effects);

        foreach (var track in setup.Tracks.OrderBy(track => IndexOf(grand.Tracks, track.Track)))
        {
            var subdoctrine = track.Subdoctrine;
            yield return (subdoctrine.ID, subdoctrine.Effects);
            var unlocked = UnlockedRewards(subdoctrine, track.Mastery, defines);

            foreach (var reward in subdoctrine.Rewards.Take(unlocked))
            {
                yield return ($"{subdoctrine.ID}/{reward.ID}", reward.Effects);
            }

            if (unlocked == subdoctrine.Rewards.Count && grand.Milestones.TryGetValue(track.Track, out var milestone))
            {
                yield return ($"{grand.ID}/{track.Track} milestone", milestone);
            }
        }
    }

    /// <summary>
    /// S-SPIRIT [RE] 0x52bce0: the <c>has_doctrine</c> trigger: true for a side's grand doctrine and for every subdoctrine
    /// slotted in one of its tracks, in any folder, whatever its mastery.
    /// </summary>
    public static bool HasDoctrine(IEnumerable<DoctrineSetup> setups, string id)
    {
        return setups.Any(setup => setup.GrandDoctrine.ID == id || setup.Tracks.Any(track => track.Subdoctrine.ID == id));
    }

    private static int IndexOf(IReadOnlyList<string> tracks, string track)
    {
        for (var i = 0; i < tracks.Count; i++)
        {
            if (tracks[i] == track)
            {
                return i;
            }
        }

        return tracks.Count;
    }
}
