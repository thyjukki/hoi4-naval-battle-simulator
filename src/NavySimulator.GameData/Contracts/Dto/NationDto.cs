namespace NavySimulator.GameData.Contracts;

/// <summary>
/// S-NATIONAL: what a country's history and focus tree give its navy, written by <c>NavySimulator import-national</c> to
/// <c>nations/&lt;TAG&gt;.json</c>. Only national spirits, dynamic modifiers, variables and focuses with effects the
/// simulator models are kept.
/// </summary>
public class NationDto
{
    /// <summary>The country tag, e.g. <c>JAP</c>.</summary>
    public string ID { get; set; } = string.Empty;

    /// <summary>The game's name (localisation), e.g. "Japan".</summary>
    public string? Name { get; set; }

    /// <summary>The focus tree's ID, e.g. <c>japan_wtt_focus</c>.</summary>
    public string? FocusTree { get; set; }

    public List<NationalSpiritDto> Spirits { get; set; } = [];
    public List<DynamicModifierDto> DynamicModifiers { get; set; } = [];

    /// <summary>S-NATIONAL-HISTORY: the history file's effects at the 1936 start, in file order.</summary>
    public List<NationalEffectDto> History { get; set; } = [];

    /// <summary>S-NATIONAL-FOCUS: the focus tree's focuses with such effects, in tree order.</summary>
    public List<NationalFocusDto> Focuses { get; set; } = [];
}
