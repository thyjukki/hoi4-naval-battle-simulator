namespace NavySimulator.GameData.Contracts;

/// <summary>A country tag of <c>common/country_tags</c> with its name, written by <c>NavySimulator import-mios</c> to <c>countries/</c>.</summary>
public class CountryDto
{
    /// <summary>The tag, e.g. <c>USA</c>.</summary>
    public string ID { get; set; } = string.Empty;

    /// <summary>The game's name (localisation), e.g. "America".</summary>
    public string? Name { get; set; }
}
