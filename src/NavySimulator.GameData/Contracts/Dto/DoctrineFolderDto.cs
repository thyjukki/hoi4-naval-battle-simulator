namespace NavySimulator.GameData.Contracts;

/// <summary>
/// One doctrine folder of <c>common/doctrines</c> (e.g. <c>naval</c>): its tracks, grand doctrines and subdoctrines, as
/// written by <c>NavySimulator import-doctrines</c> to <c>doctrines/&lt;folder&gt;.json</c> (S-DOCTRINE).
/// </summary>
public class DoctrineFolderDto
{
    public string ID { get; set; } = string.Empty;

    /// <summary>The game's name (localisation), e.g. "Naval Doctrine".</summary>
    public string? Name { get; set; }

    public List<DoctrineTrackDto> Tracks { get; set; } = [];
    public List<GrandDoctrineDto> GrandDoctrines { get; set; } = [];
    public List<SubdoctrineDto> Subdoctrines { get; set; } = [];
}
