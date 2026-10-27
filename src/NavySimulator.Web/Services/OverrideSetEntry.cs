using NavySimulator.GameData.Contracts;

namespace NavySimulator.Web.Services;

/// <summary>An override set held by the GUI: its file, the set being edited and what the file last held.</summary>
public sealed class OverrideSetEntry(string file, OverrideSetDto set, string? savedJson)
{
    /// <summary>Data-directory relative, e.g. <c>overrides/cl-buff.json</c>.</summary>
    public string File { get; internal set; } = file;

    public OverrideSetDto Set { get; internal set; } = set;

    /// <summary>The set as last read or written; null for a set never saved.</summary>
    public string? SavedJson { get; internal set; } = savedJson;

    public bool IsDirty => SavedJson != Workspace.Json(Set);

    public string DisplayName => string.IsNullOrWhiteSpace(Set.Name) ? Set.ID : Set.Name;

    public int ChangeCount => Set.Defines.Count + Set.StaticModifiers.Sum(block => block.Value.Count) + Set.Content.Count;
}
