using System.Text.Json;

namespace NavySimulator.Web.Services;

/// <summary>Reads and writes <see cref="GuiSettings"/> as JSON in the user's local application data folder.</summary>
public sealed class GuiSettingsStore
{
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true, PropertyNameCaseInsensitive = true };

    public GuiSettingsStore(string? path = null)
    {
        Path = path ?? System.IO.Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "NavySimulator", "gui-settings.json");
    }

    public string Path { get; }

    public GuiSettings Load()
    {
        try
        {
            return File.Exists(Path) ? JsonSerializer.Deserialize<GuiSettings>(File.ReadAllText(Path), JsonOptions) ?? new GuiSettings() : new GuiSettings();
        }
        catch (Exception exception) when (exception is IOException or JsonException or UnauthorizedAccessException)
        {
            return new GuiSettings();
        }
    }

    public void Save(GuiSettings settings)
    {
        Directory.CreateDirectory(System.IO.Path.GetDirectoryName(Path)!);
        File.WriteAllText(Path, JsonSerializer.Serialize(settings, JsonOptions));
    }
}
