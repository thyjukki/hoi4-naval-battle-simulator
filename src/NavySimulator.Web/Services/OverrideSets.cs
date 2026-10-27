using System.Text.Json;
using System.Text.Json.Nodes;
using NavySimulator.GameData.Contracts;
using NavySimulator.GameData.Overrides;

namespace NavySimulator.Web.Services;

/// <summary>
/// The override sets of the data directory's <c>overrides/</c> folder, edited in memory like the scenario: battles use
/// the active set with its unsaved changes, <see cref="Save"/> writes a set to its file. Every change raises
/// <see cref="Version"/>, which the workspace uses to rebuild the content the active set changes.
/// </summary>
public sealed class OverrideSets
{
    private readonly string dataDirectory;
    private readonly List<OverrideSetEntry> entries = [];

    public OverrideSets(string dataDirectory)
    {
        this.dataDirectory = dataDirectory;
        Reload();
    }

    public IReadOnlyList<OverrideSetEntry> Entries => entries;

    /// <summary>Files that could not be read, with the reason.</summary>
    public IReadOnlyList<string> ReadErrors { get; private set; } = [];

    public int Version { get; private set; }

    public OverrideSetEntry? Find(string? file)
    {
        return file is null ? null : entries.FirstOrDefault(entry => SameFile(entry.File, file));
    }

    /// <summary>Reads every set file again; unsaved sets and changes are dropped.</summary>
    public void Reload()
    {
        entries.Clear();
        var errors = new List<string>();

        foreach (var file in OverrideSetStore.ListFiles(dataDirectory))
        {
            try
            {
                var set = OverrideSetStore.Read(Path.Combine(dataDirectory, file));
                entries.Add(new OverrideSetEntry(file, set, Workspace.Json(set)));
            }
            catch (Exception exception) when (exception is IOException or JsonException or InvalidDataException)
            {
                errors.Add($"{file}: {exception.Message}");
            }
        }

        ReadErrors = errors;
        Edited();
    }

    /// <summary>A new, unsaved set; its file is named after the ID (made unique).</summary>
    public OverrideSetEntry Create(string id, OverrideSetDto? copyOf = null)
    {
        var set = copyOf is null ? new OverrideSetDto() : Workspace.Clone(copyOf);
        set.ID = UniqueId(string.IsNullOrWhiteSpace(id) ? "override-set" : id.Trim());
        set.Name = copyOf is null ? null : $"{(string.IsNullOrWhiteSpace(copyOf.Name) ? copyOf.ID : copyOf.Name)} (copy)";
        var entry = new OverrideSetEntry(OverrideSetStore.FileFor(set.ID), set, null);
        entries.Add(entry);
        Edited();
        return entry;
    }

    /// <summary>Writes the set. A changed ID moves it to a file named after the new ID.</summary>
    public void Save(OverrideSetEntry entry)
    {
        if (string.IsNullOrWhiteSpace(entry.Set.ID))
        {
            throw new InvalidOperationException("The override set needs an ID.");
        }

        if (entries.Any(other => other != entry && string.Equals(other.Set.ID, entry.Set.ID, StringComparison.Ordinal)))
        {
            throw new InvalidOperationException($"Another override set already has the ID '{entry.Set.ID}'.");
        }

        var file = OverrideSetStore.FileFor(entry.Set.ID);

        if (!SameFile(file, entry.File) && entries.Any(other => other != entry && SameFile(other.File, file)))
        {
            throw new InvalidOperationException($"{file} belongs to another override set.");
        }

        OverrideSetStore.Write(dataDirectory, file, entry.Set);

        if (!SameFile(file, entry.File) && entry.SavedJson is not null)
        {
            OverrideSetStore.Delete(dataDirectory, entry.File);
        }

        entry.File = file;
        entry.SavedJson = Workspace.Json(entry.Set);
        Edited();
    }

    public void Revert(OverrideSetEntry entry)
    {
        if (entry.SavedJson is null)
        {
            entries.Remove(entry);
        }
        else
        {
            entry.Set = OverrideSetStore.Read(Path.Combine(dataDirectory, entry.File));
            entry.SavedJson = Workspace.Json(entry.Set);
        }

        Edited();
    }

    public void Delete(OverrideSetEntry entry)
    {
        if (entry.SavedJson is not null)
        {
            OverrideSetStore.Delete(dataDirectory, entry.File);
        }

        entries.Remove(entry);
        Edited();
    }

    /// <summary>Call after changing a set in place.</summary>
    public void Edited()
    {
        Version++;
    }

    /// <summary>Sets a define (<c>NNavy.KEY</c>); null removes it from the set.</summary>
    public void SetDefine(OverrideSetDto set, string key, JsonNode? value)
    {
        if (value is null)
        {
            set.Defines.Remove(key);
        }
        else
        {
            set.Defines[key] = value.DeepClone();
        }

        Edited();
    }

    /// <summary>Sets a static modifier key of a block; <paramref name="remove"/> takes it out of the set instead.</summary>
    public void SetStaticModifier(OverrideSetDto set, string block, string modifier, double? value, bool remove = false)
    {
        if (!set.StaticModifiers.TryGetValue(block, out var values))
        {
            values = new Dictionary<string, double?>(StringComparer.Ordinal);
            set.StaticModifiers[block] = values;
        }

        if (remove)
        {
            values.Remove(modifier);

            if (values.Count == 0)
            {
                set.StaticModifiers.Remove(block);
            }
        }
        else
        {
            values[modifier] = value;
        }

        Edited();
    }

    public static ContentOverrideDto? FindContent(OverrideSetDto set, string collection, string id, string path)
    {
        return set.Content.FirstOrDefault(change => Same(change, collection, id, path));
    }

    /// <summary>Sets the content change of a field (replacing an earlier one of the same field).</summary>
    public void SetContent(OverrideSetDto set, string collection, string id, string path, JsonNode? value)
    {
        if (FindContent(set, collection, id, path) is { } existing)
        {
            existing.Value = value?.DeepClone();
        }
        else
        {
            set.Content.Add(new ContentOverrideDto { Collection = collection, ID = id, Path = path, Value = value?.DeepClone() });
        }

        Edited();
    }

    public void RemoveContent(OverrideSetDto set, string collection, string id, string path)
    {
        set.Content.RemoveAll(change => Same(change, collection, id, path));
        Edited();
    }

    private static bool Same(ContentOverrideDto change, string collection, string id, string path)
    {
        return string.Equals(change.Collection, collection, StringComparison.OrdinalIgnoreCase) &&
               string.Equals(change.ID, id, StringComparison.Ordinal) &&
               string.Equals(change.Path, path, StringComparison.Ordinal);
    }

    private string UniqueId(string id)
    {
        var candidate = id;

        for (var number = 2; entries.Any(entry => entry.Set.ID == candidate || SameFile(entry.File, OverrideSetStore.FileFor(candidate))); number++)
        {
            candidate = $"{id}-{number}";
        }

        return candidate;
    }

    private static bool SameFile(string first, string second)
    {
        return string.Equals(first.Replace('\\', '/'), second.Replace('\\', '/'), StringComparison.OrdinalIgnoreCase);
    }
}
