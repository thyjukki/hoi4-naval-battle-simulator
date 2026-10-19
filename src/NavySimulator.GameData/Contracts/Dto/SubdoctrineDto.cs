namespace NavySimulator.GameData.Contracts;

/// <summary>A subdoctrine of <c>common/doctrines/subdoctrines</c>: the root of a track, with rewards unlocked by mastery.</summary>
public class SubdoctrineDto
{
    public string ID { get; set; } = string.Empty;

    /// <summary>The game's name (localisation), e.g. "Line of Battle".</summary>
    public string? Name { get; set; }

    /// <summary>The tracks it can be slotted into (<c>track</c>).</summary>
    public List<string> Tracks { get; set; } = [];

    /// <summary><c>allow_in_multiple_tracks</c>: it may sit in several tracks at once.</summary>
    public bool AllowInMultipleTracks { get; set; }

    /// <summary><c>xor</c>: subdoctrines that cannot be active in another track of the folder at the same time.</summary>
    public List<string> Xor { get; set; } = [];

    /// <summary>Its activation effects.</summary>
    public CountryEffectsDto Effects { get; set; } = new();

    /// <summary>The rewards, in unlock order.</summary>
    public List<DoctrineRewardDto> Rewards { get; set; } = [];
}
