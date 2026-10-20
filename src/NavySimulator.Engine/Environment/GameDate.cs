using System.Globalization;

namespace NavySimulator.Engine.Environment;

/// <summary>
/// [RE] The game's calendar: 365-day years (no 29 February), hours since a base (S-NIGHT: "8760 per year"). Month and
/// day count from 1, as the game's dates read (<c>1936.5.6.12</c> is 6 May 1936, 12:00).
/// </summary>
public readonly record struct GameDate(int Year, int Month, int Day, int Hour = 0)
{
    private static readonly int[] MonthDays = [31, 28, 31, 30, 31, 30, 31, 31, 30, 31, 30, 31];

    /// <summary>Days since 1 January (0-based).</summary>
    public int DayOfYear => MonthDays.Take(Month - 1).Sum() + Day - 1;

    public GameDate AddHours(int hours)
    {
        var total = (long)DayOfYear * 24 + Hour + hours;
        var year = Year + (int)Math.Floor(total / (365.0 * 24));
        total -= (long)(year - Year) * 365 * 24;
        var dayOfYear = (int)(total / 24);
        var month = 0;

        while (dayOfYear >= MonthDays[month])
        {
            dayOfYear -= MonthDays[month];
            month++;
        }

        return new GameDate(year, month + 1, dayOfYear + 1, (int)(total % 24));
    }

    /// <summary>Reads <c>1936.5.6</c>, <c>1936.5.6.12</c> (the game's format) or <c>1936-05-06</c>.</summary>
    public static bool TryParse(string? text, out GameDate date)
    {
        date = default;
        var parts = (text ?? string.Empty).Trim().Split('.', '-', ' ', 'T', ':');
        var numbers = new List<int>();

        foreach (var part in parts.Where(part => part.Length > 0))
        {
            if (!int.TryParse(part, NumberStyles.Integer, CultureInfo.InvariantCulture, out var number))
            {
                return false;
            }

            numbers.Add(number);
        }

        if (numbers.Count < 3 || numbers[1] is < 1 or > 12 || numbers[2] < 1 || numbers[2] > MonthDays[numbers[1] - 1] ||
            (numbers.Count > 3 && numbers[3] is < 0 or > 23))
        {
            return false;
        }

        date = new GameDate(numbers[0], numbers[1], numbers[2], numbers.Count > 3 ? numbers[3] : 0);
        return true;
    }

    public override string ToString()
    {
        return $"{Year}.{Month}.{Day}.{Hour}";
    }
}
