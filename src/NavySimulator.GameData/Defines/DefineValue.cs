namespace NavySimulator.GameData.Defines;

/// <summary>
/// A value from a defines lua file: number, string, boolean, or a table (array part and/or keyed part).
/// </summary>
public sealed record DefineValue(
    double? Number = null,
    string? Text = null,
    bool? Boolean = null,
    IReadOnlyList<DefineValue>? Items = null,
    IReadOnlyDictionary<string, DefineValue>? Fields = null)
{
    public bool IsTable => Items is not null || Fields is not null;

    public double[]? ToNumberArray()
    {
        if (Items is null || Items.Any(item => item.Number is null))
        {
            return null;
        }

        return Items.Select(item => item.Number!.Value).ToArray();
    }

    public override string ToString()
    {
        if (Number is not null)
        {
            return Number.Value.ToString(System.Globalization.CultureInfo.InvariantCulture);
        }

        if (Text is not null)
        {
            return $"\"{Text}\"";
        }

        if (Boolean is not null)
        {
            return Boolean.Value ? "true" : "false";
        }

        return Items is not null ? $"{{ {string.Join(", ", Items)} }}" : "{...}";
    }
}
