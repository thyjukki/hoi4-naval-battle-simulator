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
