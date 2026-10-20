using NavySimulator.Engine.Environment;
using NavySimulator.Engine.Leaders;
using NavySimulator.Engine.Stats;
using NavySimulator.GameData.Contracts;

namespace NavySimulator.GameData;

/// <summary>
/// S-REGION, S-TERRAIN, S-WEATHER: builds a scenario's <see cref="BattleLocation"/> and <see cref="WeatherSetup"/> from its
/// <c>regionID</c>, <c>provinceID</c>, <c>startDate</c>, <c>terrain</c>, <c>weather</c> and <c>arcticWater</c>, with the
/// imported <c>sea-regions/</c> and <c>naval-terrains/</c>.
/// </summary>
public static class LocationLoader
{
    /// <summary>The <c>weather</c> value that asks for a pattern simulated from the region's weather periods.</summary>
    public const string Simulated = "simulated";

    /// <summary>The <c>terrain</c> value (like empty) that takes the region's own naval terrain.</summary>
    public const string RegionTerrain = "region";

    /// <summary>The terrain a region without <c>naval_terrain</c> fights on [RE] 0x15a5560.</summary>
    public const string DefaultTerrain = "ocean";

    /// <summary>The <c>weather</c> values of a fixed weather, by the script's phenomenon keys.</summary>
    public static readonly IReadOnlyDictionary<string, Weather> FixedWeathers = new Dictionary<string, Weather>(StringComparer.OrdinalIgnoreCase)
    {
        ["clear"] = Weather.None,
        ["no_phenomenon"] = Weather.None,
        ["rain_light"] = Weather.RainLight,
        ["rain_heavy"] = Weather.RainHeavy,
        ["snow"] = Weather.Snow,
        ["blizzard"] = Weather.Blizzard,
        ["sandstorm"] = Weather.Sandstorm
    };

    public static (BattleLocation Location, WeatherSetup Weather) Build(
        BattleScenarioDto scenario,
        ContentCatalog catalog,
        List<string> errors,
        List<string> warnings)
    {
        var terrains = catalog.NavalTerrains.GroupBy(terrain => terrain.ID, StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => CreateTerrain(group.Last()), StringComparer.Ordinal);
        SeaRegion? region = null;
        SeaProvince? province = null;

        if (scenario.RegionID is { } regionId)
        {
            if (catalog.SeaRegions.LastOrDefault(item => item.ID == regionId) is { } regionDto)
            {
                region = CreateRegion(regionDto, terrains);
            }
            else
            {
                errors.Add(catalog.SeaRegions.Count == 0
                    ? $"battleScenario.regionID {regionId}: the data directory has no sea regions (import them with 'NavySimulator import-regions')."
                    : $"battleScenario.regionID references unknown sea region {regionId}.");
            }
        }

        if (scenario.ProvinceID is { } provinceId)
        {
            if (region is null && scenario.RegionID is null)
            {
                // The province picks its region.
                if (catalog.SeaRegions.LastOrDefault(item => item.Provinces.Any(candidate => candidate.ID == provinceId)) is { } regionDto)
                {
                    region = CreateRegion(regionDto, terrains);
                }
            }

            province = region?.Provinces.FirstOrDefault(candidate => candidate.ID == provinceId);

            if (province is null && (region is not null || scenario.RegionID is null))
            {
                errors.Add(region is null
                    ? $"battleScenario.provinceID {provinceId} is not a sea province of any imported region."
                    : $"battleScenario.provinceID {provinceId} is not a sea province of region {region.ID}.");
            }
        }
        else
        {
            province = region?.Provinces.FirstOrDefault();
        }

        GameDate? startDate = null;

        if (!string.IsNullOrWhiteSpace(scenario.StartDate))
        {
            if (GameDate.TryParse(scenario.StartDate, out var date))
            {
                startDate = date;
            }
            else
            {
                errors.Add($"battleScenario.startDate '{scenario.StartDate}' is not a date (write it as the game does, e.g. 1936.5.6).");
            }
        }

        var terrain = ResolveTerrain(scenario.Terrain, region, terrains, errors);
        var location = new BattleLocation(region, province, terrain, startDate);
        var weather = BuildWeather(scenario, location, errors, warnings);
        return (location, weather);
    }

    private static NavalTerrain? ResolveTerrain(string? requested, SeaRegion? region, Dictionary<string, NavalTerrain> terrains, List<string> errors)
    {
        if (string.IsNullOrWhiteSpace(requested) || requested.Equals(RegionTerrain, StringComparison.OrdinalIgnoreCase))
        {
            return region?.NavalTerrain;
        }

        if (terrains.TryGetValue(requested, out var terrain))
        {
            return terrain;
        }

        // Data imported before naval terrains existed: ocean has no effect, as before.
        if (terrains.Count == 0 && requested.Equals(DefaultTerrain, StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        errors.Add(terrains.Count == 0
            ? $"battleScenario.terrain '{requested}': the data directory has no naval terrains (import them with 'NavySimulator import-regions')."
            : $"battleScenario.terrain references unknown naval terrain '{requested}' (known: {string.Join(", ", terrains.Keys.Order(StringComparer.Ordinal))}).");
        return null;
    }

    private static WeatherSetup BuildWeather(BattleScenarioDto scenario, BattleLocation location, List<string> errors, List<string> warnings)
    {
        var text = string.IsNullOrWhiteSpace(scenario.Weather) ? "clear" : scenario.Weather.Trim();
        var arctic = scenario.ArcticWater == true;

        if (text.Equals(Simulated, StringComparison.OrdinalIgnoreCase))
        {
            if (location.Region is null || location.StartDate is null)
            {
                warnings.Add("battleScenario.weather is 'simulated' but the scenario has no regionID and startDate; the weather stays clear.");
            }
            else if (location.Region.Weather.Count == 0)
            {
                warnings.Add($"battleScenario.weather is 'simulated' but region {location.Region.ID} has no weather periods; the weather stays clear.");
            }

            if (arctic)
            {
                warnings.Add("battleScenario.arcticWater is ignored with simulated weather (the pattern decides it).");
            }

            return new WeatherSetup(WeatherMode.Simulated, Seed: scenario.WeatherSeed);
        }

        if (!FixedWeathers.TryGetValue(text, out var phenomenon))
        {
            errors.Add($"battleScenario.weather '{scenario.Weather}' is not a weather (clear, rain_light, rain_heavy, snow, blizzard, sandstorm or simulated).");
            return WeatherSetup.Clear;
        }

        return new WeatherSetup(WeatherMode.Fixed, new RegionWeather(phenomenon, arctic));
    }

    public static NavalTerrain CreateTerrain(NavalTerrainDto dto)
    {
        return new NavalTerrain(
            dto.ID,
            new ModifierSet(dto.Modifiers ?? []),
            (dto.SubUnitModifiers ?? []).ToDictionary(entry => entry.Key, entry => new ModifierSet(entry.Value ?? []), StringComparer.Ordinal),
            (dto.SubUnitAdjusters ?? []).ToDictionary(entry => entry.Key,
                entry => new LeaderAdjuster(entry.Value?.Attack ?? 0, entry.Value?.Defence ?? 0, entry.Value?.Movement ?? 0), StringComparer.Ordinal));
    }

    public static SeaRegion CreateRegion(SeaRegionDto dto, IReadOnlyDictionary<string, NavalTerrain> terrains)
    {
        var terrainId = string.IsNullOrWhiteSpace(dto.NavalTerrain) || !terrains.ContainsKey(dto.NavalTerrain) ? DefaultTerrain : dto.NavalTerrain;
        return new SeaRegion(
            dto.ID,
            dto.Name ?? $"Region {dto.ID}",
            terrains.GetValueOrDefault(terrainId),
            (dto.Provinces ?? []).Select(province => new SeaProvince(province.ID, province.X, province.Y)).ToList(),
            (dto.Weather ?? []).Select(period => new WeatherPeriod(period.FromDay, period.FromMonth, period.ToDay, period.ToMonth,
                period.MinTemperature, period.MaxTemperature, period.NoPhenomenon, period.RainLight, period.RainHeavy, period.Snow,
                period.Blizzard, period.ArcticWater, period.Sandstorm)).ToList(),
            dto.MapWidth,
            dto.MapHeight,
            dto.WeatherStates,
            dto.BoxX,
            dto.BoxY,
            dto.BoxWidth,
            dto.BoxHeight);
    }
}
