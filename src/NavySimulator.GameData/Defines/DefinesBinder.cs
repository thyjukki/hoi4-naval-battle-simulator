using System.Reflection;
using NavySimulator.Engine.Defines;

namespace NavySimulator.GameData.Defines;

/// <summary>
/// Copies values from a parsed <see cref="DefinesTable"/> onto <see cref="GameDefines"/>, matching
/// category and key names exactly (e.g. <c>NNavy.COMBAT_BASE_HIT_CHANCE</c>).
/// </summary>
public static class DefinesBinder
{
    public static DefinesBindingReport Apply(DefinesTable table, GameDefines defines)
    {
        var report = new DefinesBindingReport();
        var targets = new Dictionary<string, object>(StringComparer.Ordinal)
        {
            ["NNavy"] = defines.NNavy,
            ["NAir"] = defines.NAir,
            ["NMilitary"] = defines.NMilitary,
            ["NDoctrines"] = defines.NDoctrines
        };

        foreach (var category in table.Categories)
        {
            var entries = table.GetCategory(category);

            if (!targets.TryGetValue(category, out var target))
            {
                report.IgnoredKeyCount += entries.Count;
                continue;
            }

            foreach (var entry in entries)
            {
                var property = target.GetType().GetProperty(entry.Key, BindingFlags.Public | BindingFlags.Instance);

                if (property is null)
                {
                    report.IgnoredKeyCount++;
                    continue;
                }

                if (TryConvert(entry.Value, property.PropertyType, out var converted))
                {
                    property.SetValue(target, converted);
                    report.Applied.Add($"{category}.{entry.Key} = {entry.Value}");
                }
                else
                {
                    report.TypeMismatches.Add($"{category}.{entry.Key}: expected {property.PropertyType.Name}, got {entry.Value}");
                }
            }
        }

        return report;
    }

    private static bool TryConvert(DefineValue value, Type targetType, out object? converted)
    {
        converted = null;

        if (targetType == typeof(double) && value.Number is not null)
        {
            converted = value.Number.Value;
        }
        else if (targetType == typeof(double[]) && value.ToNumberArray() is { } array)
        {
            converted = array;
        }
        else if (targetType == typeof(bool) && value.Boolean is not null)
        {
            converted = value.Boolean.Value;
        }
        else if (targetType == typeof(string) && value.Text is not null)
        {
            converted = value.Text;
        }

        return converted is not null;
    }
}
