using System.Globalization;
using NavySimulator.GameData.Contracts;
using NavySimulator.GameData.Paradox;

namespace NavySimulator.GameData.Import;

/// <summary>
/// Reads where naval battles are fought from the game's files [PDX]: the naval terrains of <c>common/terrain</c>
/// (<c>categories</c> entries with <c>naval_terrain = yes</c>), and every strategic region of <c>map/strategicregions</c>
/// with sea provinces (<c>map/definition.csv</c>), with its <c>naval_terrain</c>, weather periods and the provinces' map
/// positions (S-REGION, S-TERRAIN, S-WEATHER-REGION, S-NIGHT).
/// </summary>
public static class RegionImporter
{
    // Terrain keys that are not modifiers.
    private static readonly HashSet<string> TerrainSettingKeys = new(
        ["naval_terrain", "is_water", "movement_cost", "sound_type", "minimum_seazone_dominance", "color", "enemy_army_bonus_air_superiority_factor", "supply_flow_penalty_factor"],
        StringComparer.Ordinal);

    public static RegionImport Read(GameFileSystem files, string language = "english")
    {
        var import = new RegionImport();
        var localisation = LocalisationTable.Load(files, language);

        ReadTerrains(files, localisation, import);
        var seaProvinces = ReadSeaProvinces(files, import);
        var positions = ProvincePositions.Read(files, import.Warnings);
        var (width, height) = ProvincePositions.MapSize(files, import.Warnings);
        var weatherStates = ReadRegions(files, localisation, seaProvinces, positions, import);

        foreach (var region in import.Regions)
        {
            region.MapWidth = width;
            region.MapHeight = height;
            region.WeatherStates = weatherStates;
        }
        return import;
    }

    private static void ReadTerrains(GameFileSystem files, LocalisationTable localisation, RegionImport import)
    {
        var terrains = new Dictionary<string, NavalTerrainDto>(StringComparer.Ordinal);

        foreach (var path in files.GetFiles("common/terrain", "*.txt"))
        {
            foreach (var entry in Parse(files, path, import)?.GetBlock("categories")?.KeyedEntries ?? [])
            {
                if (entry.Value.Block is not { } block || block.GetBool("naval_terrain") != true)
                {
                    continue;
                }

                var terrain = new NavalTerrainDto { ID = entry.Key!, Name = localisation.Get(entry.Key!) };

                foreach (var item in block.KeyedEntries.Where(item => !TerrainSettingKeys.Contains(item.Key!)))
                {
                    if (item.Value.TryGetDouble(out var value))
                    {
                        terrain.Modifiers[item.Key!] = value;
                    }
                    else if (item.Value.Block is { } subUnit)
                    {
                        var numbers = Numbers(subUnit);

                        if (numbers.Count > 0)
                        {
                            terrain.SubUnitModifiers[item.Key!] = numbers;
                        }

                        if (subUnit.GetBlock("units") is { } units)
                        {
                            terrain.SubUnitAdjusters[item.Key!] = new LeaderAdjusterDto
                            {
                                Attack = units.GetDouble("attack") ?? 0,
                                Defence = units.GetDouble("defence") ?? 0,
                                Movement = units.GetDouble("movement") ?? 0
                            };
                        }
                    }
                }

                // A later file's terrain replaces an earlier one.
                terrains[terrain.ID] = terrain;
            }
        }

        import.Terrains.AddRange(terrains.Values.OrderBy(terrain => terrain.ID, StringComparer.Ordinal));
    }

    private static HashSet<int> ReadSeaProvinces(GameFileSystem files, RegionImport import)
    {
        var sea = new HashSet<int>();
        var definition = files.GetFiles("map", "definition.csv").FirstOrDefault();

        if (definition is null)
        {
            import.Warnings.Add("map/definition.csv not found: no sea provinces.");
            return sea;
        }

        // id;r;g;b;type;coastal;terrain;continent
        foreach (var line in File.ReadLines(definition))
        {
            var fields = line.Split(';');

            if (fields.Length > 4 && fields[4].Trim() == "sea" && int.TryParse(fields[0], NumberStyles.Integer, CultureInfo.InvariantCulture, out var id))
            {
                sea.Add(id);
            }
        }

        return sea;
    }

    // Returns the highest region id + 1, land regions included (the game's weather state count).
    private static int ReadRegions(
        GameFileSystem files,
        LocalisationTable localisation,
        HashSet<int> seaProvinces,
        IReadOnlyDictionary<int, (double X, double Y)> positions,
        RegionImport import)
    {
        var regions = new SortedDictionary<int, SeaRegionDto>();
        var highestId = 0;

        foreach (var path in files.GetFiles("map/strategicregions", "*.txt"))
        {
            foreach (var value in Parse(files, path, import)?.GetAll("strategic_region").Where(value => value.Block is not null) ?? [])
            {
                var block = value.Block!;

                if (block.GetDouble("id") is not { } number)
                {
                    import.Warnings.Add($"{files.RelativePath(path)}: a strategic region has no id.");
                    continue;
                }

                var id = (int)number;
                highestId = Math.Max(highestId, id);
                var provinces = (block.GetBlock("provinces")?.BareValues ?? [])
                    .Select(province => int.TryParse(province.Scalar, NumberStyles.Integer, CultureInfo.InvariantCulture, out var province1) ? province1 : -1)
                    .Where(seaProvinces.Contains)
                    .ToList();

                if (provinces.Count == 0)
                {
                    continue;
                }

                if (regions.ContainsKey(id))
                {
                    import.Warnings.Add($"{files.RelativePath(path)}: strategic region {id} is defined twice; the later one is kept.");
                }

                var region = new SeaRegionDto
                {
                    ID = id,
                    Name = localisation.Get($"STRATEGICREGION_{id}") ?? block.GetString("name"),
                    NavalTerrain = block.GetString("naval_terrain")
                };

                foreach (var province in provinces)
                {
                    if (!positions.TryGetValue(province, out var position))
                    {
                        import.Warnings.Add($"{files.RelativePath(path)}: province {province} has no map position; its darkness is computed at 0, 0.");
                    }

                    region.Provinces.Add(new SeaProvinceDto { ID = province, X = position.X, Y = position.Y });
                }

                foreach (var period in block.GetBlock("weather")?.GetAll("period").Where(period => period.Block is not null) ?? [])
                {
                    region.Weather.Add(ReadPeriod(period.Block!));
                }

                regions[id] = region;
            }
        }

        import.Regions.AddRange(regions.Values);
        return highestId + 1;
    }

    private static WeatherPeriodDto ReadPeriod(PdxBlock block)
    {
        // between = { day.month day.month }, both from 0.
        var between = block.GetBlock("between")?.BareValues.Select(value => value.Scalar ?? string.Empty).ToList() ?? [];
        var (fromDay, fromMonth) = DayMonth(between.ElementAtOrDefault(0));
        var (toDay, toMonth) = DayMonth(between.ElementAtOrDefault(1));
        var temperature = block.GetBlock("temperature")?.BareValues.Select(value => value.TryGetDouble(out var number) ? number : 0).ToList() ?? [];

        return new WeatherPeriodDto
        {
            FromDay = fromDay,
            FromMonth = fromMonth,
            ToDay = toDay,
            ToMonth = toMonth,
            MinTemperature = temperature.ElementAtOrDefault(0),
            MaxTemperature = temperature.ElementAtOrDefault(1),
            NoPhenomenon = block.GetDouble("no_phenomenon") ?? 0,
            RainLight = block.GetDouble("rain_light") ?? 0,
            RainHeavy = block.GetDouble("rain_heavy") ?? 0,
            Snow = block.GetDouble("snow") ?? 0,
            Blizzard = block.GetDouble("blizzard") ?? 0,
            ArcticWater = block.GetDouble("arctic_water") ?? 0,
            Mud = block.GetDouble("mud") ?? 0,
            Sandstorm = block.GetDouble("sandstorm") ?? 0,
            MinSnowLevel = block.GetDouble("min_snow_level") ?? 0
        };
    }

    private static (int Day, int Month) DayMonth(string? text)
    {
        var parts = (text ?? string.Empty).Split('.');
        var day = parts.Length > 0 && int.TryParse(parts[0], NumberStyles.Integer, CultureInfo.InvariantCulture, out var d) ? d : 0;
        var month = parts.Length > 1 && int.TryParse(parts[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out var m) ? m : 0;
        return (day, month);
    }

    private static Dictionary<string, double> Numbers(PdxBlock block)
    {
        var numbers = new Dictionary<string, double>(StringComparer.Ordinal);

        foreach (var entry in block.KeyedEntries)
        {
            if (entry.Value.TryGetDouble(out var value))
            {
                numbers[entry.Key!] = numbers.GetValueOrDefault(entry.Key!) + value;
            }
        }

        return numbers;
    }

    private static PdxBlock? Parse(GameFileSystem files, string path, RegionImport import)
    {
        try
        {
            var recovered = new List<string>();
            var block = PdxParser.ParseFile(path, recovered);
            import.Warnings.AddRange(recovered.Select(problem => $"{files.RelativePath(path)} {problem}."));
            return block;
        }
        catch (PdxParseException exception)
        {
            import.Warnings.Add($"{files.RelativePath(path)} could not be read: {exception.Message}");
            return null;
        }
    }
}
