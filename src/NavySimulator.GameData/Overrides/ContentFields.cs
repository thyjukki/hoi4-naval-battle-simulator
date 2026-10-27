using System.Reflection;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace NavySimulator.GameData.Overrides;

/// <summary>
/// Lists the fields of an item an override set can change, walking its JSON along its DTO type: numbers, flags, texts
/// and value lists as leaves, dictionaries as containers (whose keys are leaves too), and lists of items with an ID
/// entered by <c>[id]</c>. The item's own ID is left out.
/// </summary>
public static class ContentFields
{
    public static IReadOnlyList<ContentField> Describe(object item)
    {
        var fields = new List<ContentField>();
        WalkObject(OverrideApplier.ToJson(item), item.GetType(), [], fields);
        return fields;
    }

    private static void Walk(JsonNode? node, Type type, List<ContentPathSegment> path, List<ContentField> fields)
    {
        type = Nullable.GetUnderlyingType(type) ?? type;
        var joined = ContentPath.Join(path);

        if (DictionaryValueType(type) is { } valueType)
        {
            fields.Add(new ContentField(joined, ContentFieldKind.Dictionary, node));

            foreach (var (key, value) in node as JsonObject ?? [])
            {
                if (key.IndexOfAny(['.', '[', ']']) < 0)
                {
                    Walk(value, valueType, [.. path, new ContentPathSegment(key)], fields);
                }
            }
        }
        else if (type == typeof(bool))
        {
            fields.Add(new ContentField(joined, ContentFieldKind.Boolean, node));
        }
        else if (type == typeof(string) || type.IsEnum)
        {
            fields.Add(new ContentField(joined, ContentFieldKind.Text, node));
        }
        else if (type.IsPrimitive || type == typeof(decimal))
        {
            fields.Add(new ContentField(joined, ContentFieldKind.Number, node));
        }
        else if (ListItemType(type) is not null)
        {
            fields.Add(new ContentField(joined, ContentFieldKind.List, node));
        }
        else if (node is JsonObject json)
        {
            WalkObject(json, type, path, fields);
        }
    }

    private static void WalkObject(JsonObject json, Type type, List<ContentPathSegment> path, List<ContentField> fields)
    {
        foreach (var property in type.GetProperties(BindingFlags.Public | BindingFlags.Instance).Where(property => property.CanRead && property.CanWrite))
        {
            if (property.Name == "ID")
            {
                continue;
            }

            var name = JsonNamingPolicy.CamelCase.ConvertName(property.Name);
            var value = json[name];

            if (ListItemType(property.PropertyType) is { } itemType && itemType.GetProperty("ID") is not null && value is JsonArray entries)
            {
                foreach (var entry in entries.OfType<JsonObject>())
                {
                    if (entry["id"] is JsonValue id && id.TryGetValue<string>(out var text) && text.Length > 0 && text.IndexOfAny(['.', '[', ']']) < 0)
                    {
                        WalkObject(entry, itemType, [.. path, new ContentPathSegment(name, text)], fields);
                    }
                }

                continue;
            }

            Walk(value, property.PropertyType, [.. path, new ContentPathSegment(name)], fields);
        }
    }

    private static Type? DictionaryValueType(Type type)
    {
        return type.IsGenericType && type.GetGenericTypeDefinition() == typeof(Dictionary<,>) && type.GetGenericArguments()[0] == typeof(string)
            ? type.GetGenericArguments()[1]
            : null;
    }

    private static Type? ListItemType(Type type)
    {
        return type.IsGenericType && type.GetGenericTypeDefinition() == typeof(List<>) ? type.GetGenericArguments()[0] : null;
    }
}
