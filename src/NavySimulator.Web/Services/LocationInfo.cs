using System.Globalization;
using NavySimulator.Engine.Battles.Mechanics;
using NavySimulator.Engine.Defines;
using NavySimulator.Engine.Environment;
using NavySimulator.GameData;
using NavySimulator.GameData.Contracts;

namespace NavySimulator.Web.Services;

/// <summary>Display helpers for a scenario's sea region, battle province, naval terrain and weather (S-REGION, S-NIGHT, S-WEATHER, S-TERRAIN).</summary>
public static class LocationInfo
{
    private static readonly string[] MonthNames = CultureInfo.InvariantCulture.DateTimeFormat.MonthNames;

    /// <summary>The scenario's weather choices: value and label.</summary>
    public static IReadOnlyList<(string Value, string Label)> WeatherChoices { get; } =
    [
        ("clear", "Clear (no weather)"), ("rain_light", "Light rain"), ("rain_heavy", "Heavy rain"), ("snow", "Snow"),
        ("blizzard", "Blizzard"), ("sandstorm", "Sandstorm"), (LocationLoader.Simulated, "Simulated from the region")
    ];

    public static string WeatherLabel(Weather weather)
    {
        return weather switch
        {
            Weather.RainLight => "light rain",
            Weather.RainHeavy => "heavy rain",
            Weather.Snow => "snow",
            Weather.Blizzard => "blizzard",
            Weather.Sandstorm => "sandstorm",
            _ => "no weather"
        };
    }

    /// <summary>The sea regions by name, labelled "Black Sea (30)".</summary>
    public static IReadOnlyList<(int ID, string Label)> Regions(ContentCatalog catalog)
    {
        return catalog.SeaRegions
            .Select(region => (region.ID, Label: $"{region.Name ?? "Region"} ({region.ID})"))
            .OrderBy(region => region.Label, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    /// <summary>The scenario's weather as a <see cref="WeatherChoices"/> value (empty is clear).</summary>
    public static string WeatherChoice(BattleScenarioDto scenario)
    {
        return string.IsNullOrWhiteSpace(scenario.Weather) ? "clear" : scenario.Weather;
    }

    /// <summary>S-WEATHER-SIM: the hours between the region's chances to change its weather.</summary>
    public static int VisitHours(SeaRegionDto region, GameDefines defines)
    {
        return WeatherRules.VisitHours(region.WeatherStates, defines.Weather);
    }

    /// <summary>The naval terrain the scenario fights on: its own, else the region's (ocean when it sets none); null without either.</summary>
    public static NavalTerrainDto? Terrain(ContentCatalog catalog, BattleScenarioDto scenario)
    {
        var id = string.IsNullOrWhiteSpace(scenario.Terrain) || scenario.Terrain.Equals(LocationLoader.RegionTerrain, StringComparison.OrdinalIgnoreCase)
            ? Region(catalog, scenario.RegionID) is { } region ? region.NavalTerrain ?? LocationLoader.DefaultTerrain : null
            : scenario.Terrain;
        return id is null ? null : catalog.NavalTerrains.LastOrDefault(terrain => terrain.ID == id);
    }

    /// <summary>The label of the region's own terrain in the terrain picker.</summary>
    public static string RegionTerrainLabel(SeaRegionDto? region)
    {
        return region is null ? "none" : region.NavalTerrain ?? LocationLoader.DefaultTerrain;
    }

    public static SeaRegionDto? Region(ContentCatalog catalog, int? regionId)
    {
        return regionId is { } id ? catalog.SeaRegions.LastOrDefault(region => region.ID == id) : null;
    }

    /// <summary>
    /// The weather period of the start date and each weather's chance at a roll, e.g. "1–31 May, 12 to 30 °C: no weather 59 %,
    /// light rain 29 %, heavy rain 12 %"; null without a period.
    /// </summary>
    public static string? PeriodSummary(SeaRegionDto region, string? startDate, GameDefines defines)
    {
        if (!GameDate.TryParse(startDate, out var date))
        {
            return null;
        }

        var engineRegion = LocationLoader.CreateRegion(region, new Dictionary<string, NavalTerrain>());

        if (WeatherRules.PeriodOf(engineRegion, date) is not { } period)
        {
            return $"No weather period holds {date.Day} {MonthNames[date.Month - 1]}: the simulated weather stays clear.";
        }

        var chances = WeatherRules.Chances(period, defines.Weather)
            .Where(entry => entry.Chance > 0)
            .OrderByDescending(entry => entry.Chance)
            .Select(entry => $"{WeatherLabel(entry.Weather)} {entry.Chance:P0}");
        var arctic = period.ArcticWater > 0 ? $"; arctic water weight {period.ArcticWater:0.##}" : string.Empty;
        return $"{period.FromDay + 1}–{period.ToDay + 1} {MonthNames[period.ToMonth]}, {period.MinTemperature:0.#} to " +
               $"{period.MaxTemperature:0.#} °C: {string.Join(", ", chances)}{arctic}";
    }

    /// <summary>
    /// The battle's dark hours on the start day, as the battle reads them (one game hour late, S-NIGHT), e.g. "dark 17:00 to
    /// 03:00, dusk 16:00 (33 %)"; null without a province on a known map or a date.
    /// </summary>
    public static string? NightSummary(SeaRegionDto region, int? provinceId, string? startDate, GameDefines defines)
    {
        var province = provinceId is { } id ? region.Provinces.FirstOrDefault(item => item.ID == id) : region.Provinces.FirstOrDefault();

        if (province is null || region.MapWidth <= 0 || region.MapHeight <= 0 || !GameDate.TryParse(startDate, out var date))
        {
            return null;
        }

        var vectors = DarknessRules.ProvinceVectors(province.X, province.Y, region.MapWidth, region.MapHeight, defines);
        var day = new GameDate(date.Year, date.Month, date.Day);
        var hours = Enumerable.Range(0, 24)
            .Select(hour =>
            {
                var read = day.AddHours(hour - 1);
                return (Hour: hour, Darkness: DarknessRules.Darkness(vectors, read.DayOfYear, read.Hour, defines));
            })
            .ToList();
        var full = hours.Where(hour => hour.Darkness >= 1).Select(hour => hour.Hour).ToList();

        if (full.Count == 0)
        {
            return "no night on the start day";
        }

        // The dark spell wraps around midnight: start at the first dark hour after a light one.
        var start = full.FirstOrDefault(hour => !full.Contains((hour + 23) % 24), full[0]);
        var end = (full.FirstOrDefault(hour => !full.Contains((hour + 1) % 24), full[^1]) + 1) % 24;
        var ramps = hours.Where(hour => hour.Darkness is > 0 and < 1).Select(hour => $"{hour.Hour:D2}:00 ({hour.Darkness:P0})").ToList();
        return $"dark {start:D2}:00 to {end:D2}:00" + (ramps.Count > 0 ? $", twilight {string.Join(", ", ramps)}" : string.Empty);
    }

    /// <summary>What a naval terrain changes, e.g. "positioning −15 %; heavy_cruiser, battleship: attack −20 %, defence −20 %, speed −20 %".</summary>
    public static string TerrainSummary(NavalTerrainDto terrain)
    {
        var parts = new List<string>();
        var own = (terrain.Modifiers ?? []).Where(entry => entry.Key is not "naval_mine_hit_chance").Select(entry => $"{entry.Key} {Percent(entry.Value)}").ToList();

        if (own.Count > 0)
        {
            parts.Add(string.Join(", ", own));
        }

        var bySubUnit = (terrain.SubUnitAdjusters ?? []).Select(entry => (entry.Key, Text: Adjuster(entry.Value)))
            .Concat((terrain.SubUnitModifiers ?? []).Select(entry => (entry.Key, Text: string.Join(", ",
                entry.Value.Where(modifier => modifier.Key is not "navy_fuel_consumption_factor").Select(modifier => $"{modifier.Key} {Percent(modifier.Value)}")))))
            .Where(entry => entry.Text.Length > 0)
            .GroupBy(entry => entry.Key, StringComparer.Ordinal)
            .Select(group => (SubUnit: group.Key, Text: string.Join(", ", group.Select(entry => entry.Text))))
            .GroupBy(entry => entry.Text, StringComparer.Ordinal)
            .Select(group => $"{string.Join(", ", group.Select(entry => entry.SubUnit))}: {group.Key}");
        parts.AddRange(bySubUnit);
        return parts.Count == 0 ? "no effect in battle" : string.Join("; ", parts);
    }

    private static string Adjuster(LeaderAdjusterDto adjuster)
    {
        var parts = new List<string>();

        if (adjuster.Attack != 0)
        {
            parts.Add($"attack {Percent(adjuster.Attack)}");
        }

        if (adjuster.Defence != 0)
        {
            parts.Add($"defence {Percent(adjuster.Defence)}");
        }

        if (adjuster.Movement != 0)
        {
            parts.Add($"speed {Percent(adjuster.Movement)}");
        }

        return string.Join(", ", parts);
    }

    private static string Percent(double value)
    {
        return (value > 0 ? "+" : value < 0 ? "−" : string.Empty) + Math.Abs(value * 100).ToString("0.##", CultureInfo.InvariantCulture) + " %";
    }
}
