namespace NavySimulator.GameData.Contracts;

/// <summary>A grand doctrine of <c>common/doctrines/grand_doctrines</c>, the root of its folder (S-DOCTRINE).</summary>
public class GrandDoctrineDto
{
    public string ID { get; set; } = string.Empty;

    /// <summary>The game's name (localisation), e.g. "Fleet in Being".</summary>
    public string? Name { get; set; }

    /// <summary>The tracks it opens, in the game's order.</summary>
    public List<string> Tracks { get; set; } = [];

    /// <summary>Its activation effects.</summary>
    public CountryEffectsDto Effects { get; set; } = new();

    /// <summary>The milestone of each track (by track ID): extra effects once that track's rewards are all unlocked.</summary>
    public Dictionary<string, CountryEffectsDto> Milestones { get; set; } = [];
}
