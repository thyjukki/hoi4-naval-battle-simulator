using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;
using NavySimulator.GameData.Defines;

namespace NavySimulator.GameData.Overrides;

/// <summary>Reading, writing and comparing override values: JSON values, typed as a defines file or a data field would.</summary>
public static class OverrideValues
{
    /// <summary>
    /// Reads a value typed by the user: JSON (<c>0.25</c>, <c>true</c>, <c>[3, 6, 2]</c>, <c>"text"</c>), a Lua table of
    /// numbers (<c>{ 3, 6, 2 }</c>), or else the text itself as a string. Empty text is null.
    /// </summary>
    public static JsonNode? Parse(string? text)
    {
        var trimmed = text?.Trim() ?? string.Empty;

        if (trimmed.Length == 0)
        {
            return null;
        }

        if (trimmed.StartsWith('{') && trimmed.EndsWith('}') && !trimmed.Contains(':'))
        {
            trimmed = $"[{trimmed[1..^1]}]";
        }

        try
        {
            return JsonNode.Parse(trimmed);
        }
        catch (JsonException)
        {
            return JsonValue.Create(text!.Trim());
        }
    }

    public static string Format(JsonNode? value)
    {
        return value switch
        {
            null => "(none)",
            JsonValue scalar when scalar.GetValueKind() == JsonValueKind.Number => Format(Number(scalar)),
            JsonValue scalar when scalar.GetValueKind() == JsonValueKind.String => scalar.GetValue<string>(),
            JsonArray list when list.Count > 0 && list.All(IsNumber) => $"{{ {string.Join(", ", list.Select(entry => Format(Number(entry!))))} }}",
            _ => value.ToJsonString()
        };
    }

    public static string Format(double value)
    {
        return value.ToString("0.#########", CultureInfo.InvariantCulture);
    }

    /// <summary>A define property's value as JSON (number, boolean, string or list of numbers).</summary>
    public static JsonNode? FromDefine(object? value)
    {
        return value switch
        {
            double number => JsonValue.Create(number),
            bool flag => JsonValue.Create(flag),
            string text => JsonValue.Create(text),
            double[] numbers => new JsonArray(numbers.Select(number => (JsonNode)JsonValue.Create(number)).ToArray()),
            _ => null
        };
    }

    public static DefineValue? ToDefineValue(JsonNode? node)
    {
        return node switch
        {
            JsonValue scalar when scalar.GetValueKind() == JsonValueKind.Number => new DefineValue(Number: Number(scalar)),
            JsonValue scalar when scalar.GetValueKind() is JsonValueKind.True or JsonValueKind.False => new DefineValue(Boolean: scalar.GetValue<bool>()),
            JsonValue scalar when scalar.GetValueKind() == JsonValueKind.String => new DefineValue(Text: scalar.GetValue<string>()),
            JsonArray list when list.All(IsNumber) => new DefineValue(Items: list.Select(entry => new DefineValue(Number: Number(entry!))).ToList()),
            _ => null
        };
    }

    /// <summary>Deep equality where numbers compare by value (<c>5</c> equals <c>5.0</c>).</summary>
    public static bool Equal(JsonNode? left, JsonNode? right)
    {
        if (left is null || right is null)
        {
            return left is null && right is null;
        }

        if (IsNumber(left) && IsNumber(right))
        {
            return Number(left) == Number(right);
        }

        if (left is JsonArray leftList && right is JsonArray rightList)
        {
            return leftList.Count == rightList.Count && leftList.Zip(rightList).All(pair => Equal(pair.First, pair.Second));
        }

        if (left is JsonObject leftObject && right is JsonObject rightObject)
        {
            return leftObject.Count == rightObject.Count &&
                   leftObject.All(property => rightObject.TryGetPropertyValue(property.Key, out var other) && Equal(property.Value, other));
        }

        return JsonNode.DeepEquals(left, right);
    }

    // Through the JSON text, so a value made from any numeric type (or parsed) reads the same.
    public static double Number(JsonNode node)
    {
        return double.Parse(node.ToJsonString(), CultureInfo.InvariantCulture);
    }

    private static bool IsNumber(JsonNode? node)
    {
        return node is JsonValue value && value.GetValueKind() == JsonValueKind.Number;
    }
}
