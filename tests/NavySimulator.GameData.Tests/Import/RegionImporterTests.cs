using NavySimulator.GameData.Import;

namespace NavySimulator.GameData.Tests.Import;

/// <summary>The sea region and naval terrain import (S-REGION, S-TERRAIN) from a small game folder with a mod over it.</summary>
public sealed class RegionImporterTests : IDisposable
{
    private readonly string root = Path.Combine(Path.GetTempPath(), "navysim-regions-" + Guid.NewGuid().ToString("N"));
    private readonly string game;
    private readonly string mod;

    public RegionImporterTests()
    {
        game = Path.Combine(root, "game");
        mod = Path.Combine(root, "mod");

        Write(game, "map/definition.csv", """
            0;0;0;0;land;false;unknown;0
            1;10;10;10;land;false;plains;1
            7906;0;0;55;sea;false;ocean;0
            8256;0;0;56;sea;false;ocean;0
            """);
        Write(game, "map/unitstacks.txt", """
            7906;0;3260.00;9.50;1382.00;0.00;1.00
            7906;1;3251.00;9.50;1371.00;-0.68;1.00
            8256;0;3300.00;9.50;1371.00;0.00;1.00
            """);
        WriteBitmapHeader(game, "map/provinces.bmp", 5632, 2048);
        Write(game, "map/strategicregions/1-Land.txt", """
            strategic_region = { id = 1 name = "STRATEGICREGION_1" provinces = { 1 } }
            """);
        Write(game, "map/strategicregions/30-Black Sea.txt", """
            strategic_region = {
                id = 30
                name = "STRATEGICREGION_30"
                provinces = { 7906 8256 }
                weather = {
                    period = {
                        between = { 0.4 30.4 }
                        temperature = { 12.0 30.0 }
                        no_phenomenon = 0.5
                        rain_light = 0.25
                        rain_heavy = 0.1
                        snow = 0.0
                        blizzard = 0.0
                        arctic_water = 0.0
                        mud = 100000.0
                        sandstorm = 0.0
                        min_snow_level = 0.0
                    }
                }
            }
            """);
        Write(game, "map/strategicregions/40-North Sea.txt", """
            strategic_region = { id = 40 name = "STRATEGICREGION_40" provinces = { 8256 } naval_terrain = water_shallow_sea }
            """);
        Write(game, "common/terrain/00_terrain.txt", """
            categories = {
                ocean = { naval_terrain = yes color = { 1 2 3 } movement_cost = 1.0 is_water = yes }
                plains = { color = { 4 5 6 } movement_cost = 1.0 }
                water_shallow_sea = {
                    naval_terrain = yes
                    is_water = yes
                    submarine = { navy_visibility = 0.33 }
                    positioning = -0.05
                    minimum_seazone_dominance = 100
                }
            }
            """);
        Write(game, "localisation/english/regions_l_english.yml", """
            l_english:
             STRATEGICREGION_30: "Black Sea"
             water_shallow_sea: "Shallow Sea"
            """);
        Write(mod, "common/terrain/00_terrain.txt", """
            categories = {
                ocean = { naval_terrain = yes }
                water_shallow_sea = {
                    naval_terrain = yes
                    submarine = { navy_visibility = 1.0 }
                    destroyer = { units = { attack = -0.2 movement = -0.1 } }
                    positioning = -0.05
                }
            }
            """);
    }

    public void Dispose()
    {
        if (Directory.Exists(root))
        {
            Directory.Delete(root, true);
        }
    }

    [Fact]
    public void Read_KeepsRegionsWithSeaProvinces_WithTheirWeatherAndPositions()
    {
        var import = RegionImporter.Read(new GameFileSystem(game, [mod]));

        Assert.Equal([30, 40], import.Regions.Select(region => region.ID));
        var blackSea = import.Regions[0];
        Assert.Equal("Black Sea", blackSea.Name);
        Assert.Null(blackSea.NavalTerrain);
        Assert.Equal((5632, 2048), (blackSea.MapWidth, blackSea.MapHeight));
        Assert.Equal(41, blackSea.WeatherStates);
        Assert.Equal([(7906, 3260.0, 1382.0), (8256, 3300.0, 1371.0)], blackSea.Provinces.Select(province => (province.ID, province.X, province.Y)));
        var may = Assert.Single(blackSea.Weather);
        Assert.Equal((0, 4, 30, 4), (may.FromDay, may.FromMonth, may.ToDay, may.ToMonth));
        Assert.Equal((12.0, 30.0, 0.5, 0.25, 0.1), (may.MinTemperature, may.MaxTemperature, may.NoPhenomenon, may.RainLight, may.RainHeavy));
        Assert.Equal("water_shallow_sea", import.Regions[1].NavalTerrain);
    }

    [Fact]
    public void Read_TakesTheModsNavalTerrains()
    {
        var import = RegionImporter.Read(new GameFileSystem(game, [mod]));

        Assert.Equal(["ocean", "water_shallow_sea"], import.Terrains.Select(terrain => terrain.ID));
        var shallow = import.Terrains[1];
        Assert.Equal("Shallow Sea", shallow.Name);
        Assert.Equal(new Dictionary<string, double> { ["positioning"] = -0.05 }, shallow.Modifiers);
        Assert.Equal(1.0, shallow.SubUnitModifiers["submarine"]["navy_visibility"]);
        Assert.Equal((-0.2, 0.0, -0.1), (shallow.SubUnitAdjusters["destroyer"].Attack, shallow.SubUnitAdjusters["destroyer"].Defence, shallow.SubUnitAdjusters["destroyer"].Movement));
        Assert.False(shallow.SubUnitAdjusters.ContainsKey("submarine"));
    }

    [Fact]
    public void Write_ProducesFilesTheCatalogReads()
    {
        var data = Path.Combine(root, "data");
        Directory.CreateDirectory(data);

        var files = RegionWriter.Write(data, RegionImporter.Read(new GameFileSystem(game, [mod])));
        var catalog = new SetupLoader().ReadCatalog(data);

        Assert.Equal(["naval-terrains/naval-terrains.json", "sea-regions/sea-regions.json"], files);
        Assert.Equal([30, 40], catalog.SeaRegions.Select(region => region.ID));
        Assert.Equal(2, catalog.NavalTerrains.Count);
    }

    private static void Write(string folder, string relativePath, string text)
    {
        var path = Path.Combine(folder, relativePath);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, text);
    }

    private static void WriteBitmapHeader(string folder, string relativePath, int width, int height)
    {
        var path = Path.Combine(folder, relativePath);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        var header = new byte[54];
        header[0] = (byte)'B';
        header[1] = (byte)'M';
        BitConverter.GetBytes(width).CopyTo(header, 18);
        BitConverter.GetBytes(height).CopyTo(header, 22);
        File.WriteAllBytes(path, header);
    }
}
