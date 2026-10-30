namespace NavySimulator.GameData.Contracts;

/// <summary>S-NATIONAL-FOCUS: a national focus whose completion reward has effects the simulator follows.</summary>
public class NationalFocusDto
{
    public string ID { get; set; } = string.Empty;

    /// <summary>The game's name (localisation).</summary>
    public string? Name { get; set; }

    public List<string> MutuallyExclusive { get; set; } = [];
    public List<NationalEffectDto> Effects { get; set; } = [];
}
