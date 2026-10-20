using NavySimulator.Engine.Defines;
using NavySimulator.GameData.Defines;

namespace NavySimulator.GameData.Tests.Defines;

public class DefinesBinderTests
{
    [Fact]
    public void Apply_SetsMatchingPropertiesAndReportsTheRest()
    {
        var table = LuaDefinesParser.Parse("""
            NDefines.NNavy.COMBAT_BASE_HIT_CHANCE = 0.2
            NDefines.NNavy.GUN_HIT_PROFILES = { 70, 90, 40 }
            NDefines.NNavy.WAR_SCORE_GAIN_FOR_SUNK_CONVOY = 1
            NDefines.NNavy.COMBAT_MIN_HIT_CHANCE = "oops"
            NDefines.NCountry.SOMETHING = 1
            """);
        var defines = GameDefines.Vanilla();

        var report = DefinesBinder.Apply(table, defines);

        Assert.Equal(0.2, defines.NNavy.COMBAT_BASE_HIT_CHANCE);
        Assert.Equal([70.0, 90.0, 40.0], defines.NNavy.GUN_HIT_PROFILES);
        Assert.Equal(GameDefines.Vanilla().NNavy.COMBAT_MIN_HIT_CHANCE, defines.NNavy.COMBAT_MIN_HIT_CHANCE);
        Assert.Equal(2, report.Applied.Count);
        Assert.Single(report.TypeMismatches);
        Assert.Equal(2, report.IgnoredKeyCount);
    }

    [Fact]
    public void Load_AppliesDirectoryFilesInGameOrder()
    {
        var root = Directory.CreateTempSubdirectory("hoi4-defines-");

        try
        {
            var definesDirectory = Directory.CreateDirectory(Path.Combine(root.FullName, "common", "defines"));
            File.WriteAllText(Path.Combine(definesDirectory.FullName, "00_defines.lua"), "NDefines = { NNavy = { COMBAT_BASE_HIT_CHANCE = 0.3, COMBAT_MIN_HIT_CHANCE = 0.01 } }");
            File.WriteAllText(Path.Combine(definesDirectory.FullName, "zz_mod.lua"), "NDefines.NNavy.COMBAT_BASE_HIT_CHANCE = 0.4");

            var (defines, report) = DefinesLoader.Load([root.FullName]);

            Assert.Equal(0.4, defines.NNavy.COMBAT_BASE_HIT_CHANCE);
            Assert.Equal(0.01, defines.NNavy.COMBAT_MIN_HIT_CHANCE);
            Assert.Empty(report.TypeMismatches);
            Assert.Equal(root.FullName, defines.Sources[^1]);
        }
        finally
        {
            root.Delete(true);
        }
    }

    [Fact]
    public void Load_ReadsStaticModifierBlocksFromGameAndModRoots()
    {
        var root = Directory.CreateTempSubdirectory("hoi4-mod-");

        try
        {
            var modifiers = Directory.CreateDirectory(Path.Combine(root.FullName, "common", "modifiers"));
            File.WriteAllText(Path.Combine(modifiers.FullName, "00_static_modifiers.txt"), """
                screening_bonus = {
                    naval_hit_chance = 0.3   # modded
                }
                unrelated_block = { naval_hit_chance = 9 }
                """);

            var (defines, report) = DefinesLoader.Load([root.FullName]);

            Assert.Equal(0.3, defines.StaticModifiers.Get(StaticModifiers.ScreeningBonus, StaticModifiers.NavalHitChance));
            Assert.Equal(0, defines.StaticModifiers.Get(StaticModifiers.ScreeningBonus, StaticModifiers.NavalRetreatSpeed));
            Assert.Equal(0.2, defines.StaticModifiers.Get(StaticModifiers.CapitalScreeningBonus, StaticModifiers.NavalRetreatSpeed));
            Assert.Empty(report.ParseWarnings);
        }
        finally
        {
            root.Delete(true);
        }
    }

    [Fact]
    public void Load_ReadsGraphicsDefinesAndWeatherSettings()
    {
        var root = Directory.CreateTempSubdirectory("hoi4-weather-");

        try
        {
            var definesDirectory = Directory.CreateDirectory(Path.Combine(root.FullName, "common", "defines"));
            File.WriteAllText(Path.Combine(definesDirectory.FullName, "00_graphics.lua"), "NDefines_Graphics = { NGraphics = { GMT_OFFSET = 2800, DAY_NIGHT_FEATHER = 0.03 } }");
            File.WriteAllText(Path.Combine(root.FullName, "common", "weather.txt"), """
                settings = {
                    snowing = { temperature = { 2.0 -6.0 } }
                    arctic_water = { arctic_water_temperature = { -4.0 1.0 } }
                    duration = { no_phenomenon = { 3 9 } rain_light = { 4 8 } }
                    performance = { regions_per_update = 5 }
                }
                """);

            var (defines, _) = DefinesLoader.Load([root.FullName]);

            Assert.Equal(2800, defines.NGraphics.GMT_OFFSET);
            Assert.Equal(0.03, defines.NGraphics.DAY_NIGHT_FEATHER);
            Assert.Equal((3.0, 9.0), defines.Weather.Duration[NavySimulator.Engine.Environment.Weather.None]);
            Assert.Equal((4.0, 8.0), defines.Weather.Duration[NavySimulator.Engine.Environment.Weather.RainLight]);
            Assert.Equal((2.0, 15.0), defines.Weather.Duration[NavySimulator.Engine.Environment.Weather.RainHeavy]);
            Assert.Equal((2.0, -6.0), (defines.Weather.SnowingTemperatureNoRain, defines.Weather.SnowingTemperatureFull));
            Assert.Equal((-4.0, 1.0), defines.Weather.ArcticWaterTemperature);
            Assert.Equal(5, defines.Weather.RegionsPerUpdate);
        }
        finally
        {
            root.Delete(true);
        }
    }

    [Fact]
    public void Load_RejectsDirectoryWithoutLuaFiles()
    {
        var root = Directory.CreateTempSubdirectory("hoi4-defines-empty-");

        try
        {
            Assert.Throws<FileNotFoundException>(() => DefinesLoader.Load([root.FullName]));
        }
        finally
        {
            root.Delete(true);
        }
    }

    [Fact]
    public void TfbLegacyProfile_BindsWithoutMismatches()
    {
        var (defines, report) = DefinesLoader.Load([Path.Combine(RepositoryPaths.Data, "defines", "tfb-legacy.lua")]);

        Assert.Empty(report.TypeMismatches);
        Assert.Equal(0, report.IgnoredKeyCount);
        Assert.Equal([3.0, 5.0, 2.0], defines.NNavy.BASE_GUN_COOLDOWNS);
        Assert.Equal(9, defines.NNavy.NAVY_PIERCING_THRESHOLDS.Length);
    }
}
