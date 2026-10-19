namespace NavySimulator.GameData.Contracts;

/// <summary>A reward of a subdoctrine, unlocked by the track's mastery (S-DOCTRINE-MASTERY).</summary>
public class DoctrineRewardDto
{
    public string ID { get; set; } = string.Empty;

    /// <summary>The game's name (localisation key <c>&lt;subdoctrine&gt;_&lt;reward&gt;</c>), e.g. "Flagship Coordination".</summary>
    public string? Name { get; set; }

    /// <summary>The reward's <c>mastery</c>; null takes <c>NDoctrines.DEFAULT_REWARD_MASTERY</c>.</summary>
    public double? Mastery { get; set; }

    public CountryEffectsDto Effects { get; set; } = new();
}
