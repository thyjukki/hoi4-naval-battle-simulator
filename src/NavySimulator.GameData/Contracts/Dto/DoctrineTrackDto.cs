namespace NavySimulator.GameData.Contracts;

/// <summary>A doctrine track of <c>common/doctrines/tracks</c>: a slot for one subdoctrine and its rewards.</summary>
public class DoctrineTrackDto
{
    public string ID { get; set; } = string.Empty;

    /// <summary>The game's name (localisation), e.g. "Capital Ships".</summary>
    public string? Name { get; set; }
}
