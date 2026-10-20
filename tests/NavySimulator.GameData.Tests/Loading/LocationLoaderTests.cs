using NavySimulator.Engine.Environment;
using NavySimulator.GameData.Contracts;

namespace NavySimulator.GameData.Tests.Loading;

/// <summary>S-REGION, S-TERRAIN, S-WEATHER: a scenario's location and weather from the imported regions and terrains.</summary>
public class LocationLoaderTests
{
    [Fact]
    public void Build_TakesTheRegionsProvinceTerrainAndDate()
    {
        var (location, weather) = Build(new BattleScenarioDto { RegionID = 40, ProvinceID = 8256, StartDate = "1936.5.6", Weather = "simulated" });

        Assert.Equal(40, location.Region!.ID);
        Assert.Equal(8256, location.Province!.ID);
        Assert.Equal("water_shallow_sea", location.Terrain!.ID);
        Assert.Equal(new GameDate(1936, 5, 6), location.StartDate);
        Assert.True(location.HasTerminator);
        Assert.Equal(WeatherMode.Simulated, weather.Mode);
    }

    [Fact]
    public void Build_UsesOceanForARegionWithoutNavalTerrain_AndTheFirstProvince()
    {
        var (location, weather) = Build(new BattleScenarioDto { RegionID = 30, Weather = "rain_heavy", ArcticWater = true });

        Assert.Equal("ocean", location.Terrain!.ID);
        Assert.Equal(7906, location.Province!.ID);
        Assert.False(location.HasTerminator);
        Assert.Equal(new WeatherSetup(WeatherMode.Fixed, new RegionWeather(Weather.RainHeavy, true)), weather);
    }

    [Fact]
    public void Build_TerrainOverridesTheRegions_AndAProvinceFindsItsRegion()
    {
        var (location, _) = Build(new BattleScenarioDto { ProvinceID = 8256, Terrain = "ocean" });

        Assert.Equal(40, location.Region!.ID);
        Assert.Equal("ocean", location.Terrain!.ID);
    }

    [Fact]
    public void Build_ReportsUnknownInputs()
    {
        var errors = new List<string>();
        var catalog = Catalog();

        LocationLoader.Build(new BattleScenarioDto { RegionID = 99, StartDate = "May 1936", Terrain = "water_lava", Weather = "fog" }, catalog, errors, []);

        Assert.Equal(4, errors.Count);
        Assert.Contains(errors, error => error.Contains("unknown sea region 99"));
        Assert.Contains(errors, error => error.Contains("'May 1936' is not a date"));
        Assert.Contains(errors, error => error.Contains("unknown naval terrain 'water_lava'"));
        Assert.Contains(errors, error => error.Contains("'fog' is not a weather"));
    }

    [Fact]
    public void Build_WarnsWhenSimulatedWeatherHasNoRegion()
    {
        var warnings = new List<string>();

        var (_, weather) = LocationLoader.Build(new BattleScenarioDto { Weather = "simulated", Terrain = "ocean" }, Catalog(), [], warnings);

        Assert.Equal(WeatherMode.Simulated, weather.Mode);
        Assert.Contains(warnings, warning => warning.Contains("no regionID and startDate"));
    }

    private static (BattleLocation Location, WeatherSetup Weather) Build(BattleScenarioDto scenario)
    {
        var errors = new List<string>();
        var result = LocationLoader.Build(scenario, Catalog(), errors, []);
        Assert.Empty(errors);
        return result;
    }

    private static ContentCatalog Catalog()
    {
        var catalog = new ContentCatalog("data");
        catalog.NavalTerrains.Add(new NavalTerrainDto { ID = "ocean" });
        catalog.NavalTerrains.Add(new NavalTerrainDto { ID = "water_shallow_sea", Modifiers = new() { ["positioning"] = -0.05 } });
        catalog.SeaRegions.Add(new SeaRegionDto
        {
            ID = 30,
            Name = "Black Sea",
            Provinces = [new SeaProvinceDto { ID = 7906, X = 3260, Y = 1382 }],
            Weather = [new WeatherPeriodDto { FromMonth = 4, ToDay = 30, ToMonth = 4, NoPhenomenon = 0.5, RainLight = 0.25 }]
        });
        catalog.SeaRegions.Add(new SeaRegionDto
        {
            ID = 40,
            Name = "North Sea",
            NavalTerrain = "water_shallow_sea",
            MapWidth = 5632,
            MapHeight = 2048,
            WeatherStates = 315,
            Provinces = [new SeaProvinceDto { ID = 8256, X = 3300, Y = 1371 }]
        });
        return catalog;
    }
}
