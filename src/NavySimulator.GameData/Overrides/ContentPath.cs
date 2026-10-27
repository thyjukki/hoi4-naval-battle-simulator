using System.Text.Json.Nodes;

namespace NavySimulator.GameData.Overrides;

/// <summary>
/// The path of a field inside an item's JSON, as override sets write it: property names separated by dots, with
/// <c>[id]</c> after a list property picking the entry whose <c>id</c> is that ID
/// (<c>subdoctrines[x].rewards[y].mastery</c>). Property names match case-insensitively, dictionary keys exactly first.
/// </summary>
public static class ContentPath
{
    public static IReadOnlyList<ContentPathSegment> Parse(string path)
    {
        var segments = new List<ContentPathSegment>();
        var position = 0;

        while (position < path.Length)
        {
            var end = path.IndexOfAny(['.', '['], position);
            var name = path[position..(end < 0 ? path.Length : end)];
            string? id = null;
            position = end < 0 ? path.Length : end;

            if (position < path.Length && path[position] == '[')
            {
                var close = path.IndexOf(']', position);

                if (close < 0)
                {
                    throw new FormatException($"'{path}': '[' without ']'.");
                }

                id = path[(position + 1)..close];
                position = close + 1;
            }

            if (name.Length == 0 || id is { Length: 0 })
            {
                throw new FormatException($"'{path}': empty name or [id].");
            }

            segments.Add(new ContentPathSegment(name, id));

            if (position < path.Length)
            {
                if (path[position] != '.' || position == path.Length - 1)
                {
                    throw new FormatException($"'{path}': expected '.' after '{segments[^1]}'.");
                }

                position++;
            }
        }

        if (segments.Count == 0)
        {
            throw new FormatException("The path is empty.");
        }

        return segments;
    }

    public static string Join(IEnumerable<ContentPathSegment> segments)
    {
        return string.Join('.', segments);
    }

    /// <summary>The value at <paramref name="path"/>, or null when it (or a step to it) is missing.</summary>
    public static JsonNode? Read(JsonNode root, string path)
    {
        JsonNode? current = root;

        foreach (var segment in Parse(path))
        {
            current = Step(current as JsonObject, segment);
        }

        return current;
    }

    public static bool Exists(JsonNode root, string path)
    {
        JsonNode? current = root;
        var segments = Parse(path);

        for (var index = 0; index < segments.Count; index++)
        {
            if (current is not JsonObject parent || PropertyName(parent, segments[index].Name) is not { } name)
            {
                return false;
            }

            current = segments[index].Id is null ? parent[name] : FindEntry(parent[name] as JsonArray, segments[index].Id!);

            if (segments[index].Id is not null && current is null)
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>
    /// Sets the value at <paramref name="path"/> (null removes the property). Missing objects on the way are created; a
    /// missing list entry or a step through a non-object is an error.
    /// </summary>
    public static void Write(JsonObject root, string path, JsonNode? value)
    {
        var segments = Parse(path);
        var current = root;

        for (var index = 0; index < segments.Count - 1; index++)
        {
            var segment = segments[index];
            var name = PropertyName(current, segment.Name) ?? segment.Name;

            if (segment.Id is not null)
            {
                current = FindEntry(current[name] as JsonArray, segment.Id) as JsonObject
                          ?? throw new InvalidOperationException($"'{segment.Name}' has no entry '{segment.Id}'.");
                continue;
            }

            if (current[name] is null)
            {
                current[name] = new JsonObject();
            }

            current = current[name] as JsonObject ?? throw new InvalidOperationException($"'{ContentPath.Join(segments.Take(index + 1))}' is not an object.");
        }

        var last = segments[^1];
        var lastName = PropertyName(current, last.Name) ?? last.Name;

        if (last.Id is not null)
        {
            var list = current[lastName] as JsonArray ?? throw new InvalidOperationException($"'{last.Name}' is not a list.");
            var entry = FindEntry(list, last.Id) ?? throw new InvalidOperationException($"'{last.Name}' has no entry '{last.Id}'.");

            if (value is null)
            {
                list.Remove(entry);
            }
            else
            {
                list[list.IndexOf(entry)] = value.DeepClone();
            }

            return;
        }

        if (value is null)
        {
            current.Remove(lastName);
        }
        else
        {
            current[lastName] = value.DeepClone();
        }
    }

    private static JsonNode? Step(JsonObject? parent, ContentPathSegment segment)
    {
        if (parent is null || PropertyName(parent, segment.Name) is not { } name)
        {
            return null;
        }

        return segment.Id is null ? parent[name] : FindEntry(parent[name] as JsonArray, segment.Id);
    }

    // Dictionary keys are case-sensitive in the game, DTO properties are not: an exact match wins.
    private static string? PropertyName(JsonObject parent, string name)
    {
        if (parent.ContainsKey(name))
        {
            return name;
        }

        return parent.Select(property => property.Key).FirstOrDefault(key => string.Equals(key, name, StringComparison.OrdinalIgnoreCase));
    }

    private static JsonNode? FindEntry(JsonArray? list, string id)
    {
        return list?.FirstOrDefault(entry => entry is JsonObject item && PropertyName(item, "id") is { } key &&
                                             item[key] is JsonValue value && value.TryGetValue<string>(out var text) &&
                                             string.Equals(text, id, StringComparison.Ordinal));
    }
}
