namespace NavySimulator.Web.Services;

/// <summary>Per-user GUI preferences, kept outside the repository (see <see cref="GuiSettingsStore"/>).</summary>
public sealed class GuiSettings
{
    /// <summary>Define sources applied after the scenario's own, like the CLI's <c>--defines</c> (e.g. a mod folder).</summary>
    public List<string> ExtraDefineSources { get; set; } = [];

    /// <summary>Scenario file opened on start, relative to the data directory.</summary>
    public string? LastScenarioFile { get; set; }
}
