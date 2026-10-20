using NavySimulator.Engine.Battles;
using NavySimulator.Engine.Battles.Mechanics;
using NavySimulator.Engine.Defines;
using NavySimulator.Engine.Environment;
using NavySimulator.Engine.Tests.TestSupport;

namespace NavySimulator.Engine.Tests.Mechanics;

/// <summary>S-NIGHT [RE + LOG]: the province darkness (0xf14fe0, 0xf15eb0, 0xf20c50) against the logged TFB weather battles.</summary>
public class DarknessRulesTests
{
    private readonly GameDefines defines = GameDefines.Vanilla();

    // Province 8256 of the Black Sea (unitstacks 3300, 1371) on the 5632 × 2048 map.
    private (long X, long Y, long Z)[] Province8256() => DarknessRules.ProvinceVectors(3300, 1371, 5632, 2048, defines);

    [Theory]
    // [LOG] air_cv_mirror_weather s8: the dusk hour 16:00 from 6 May 1936, to the logged five decimals.
    [InlineData(5, 6, 16, 0.33208)]
    [InlineData(5, 7, 16, 0.30249)]
    [InlineData(5, 6, 15, 0)]
    [InlineData(5, 6, 17, 1)]
    [InlineData(5, 7, 3, 1)]
    [InlineData(5, 7, 4, 0)]
    public void Darkness_IsTheLoggedValue(int month, int day, int hour, double expected)
    {
        var dayOfYear = new GameDate(1936, month, day).DayOfYear;

        Assert.Equal(expected, DarknessRules.Darkness(Province8256(), dayOfYear, hour, defines), 5);
    }

    [Fact]
    public void Darkness_FollowsTheSeason()
    {
        var vectors = Province8256();
        var midsummer = new GameDate(1936, 6, 22).DayOfYear;
        var midwinter = new GameDate(1936, 12, 22).DayOfYear;

        // 16:00 is still day at midsummer and night at midwinter.
        Assert.Equal(0, DarknessRules.Darkness(vectors, midsummer, 16, defines));
        Assert.Equal(1, DarknessRules.Darkness(vectors, midwinter, 16, defines));
    }

    [Fact]
    public void Session_ReadsTheDarknessOfTheHourBefore()
    {
        // [LOG] Battle hour h of a battle created at 12:00 (startHourOfDay 13) logged the darkness of 12:00 + h.
        var region = new SeaRegion(30, "Black Sea", null, [new SeaProvince(8256, 3300, 1371)], [], 5632, 2048, 315);
        var location = new BattleLocation(region, region.Provinces[0], null, new GameDate(1936, 5, 6));
        var scenario = new BattleScenario("t", "ocean", "clear", 1, null,
            TestEntityFactory.Participant("a", [TestEntityFactory.Screen("a1")]), TestEntityFactory.Participant("d", [TestEntityFactory.Screen("d1")]),
            new Dictionary<string, PlaneEquipment>(), defines, new BattleRules { StartHourOfDay = 13 }, location: location);
        var session = new BattleSession(scenario, 1);

        Assert.Equal(0, session.EnvironmentAt(3).Darkness);
        Assert.Equal(0.33208, session.EnvironmentAt(4).Darkness, 5);
        Assert.Equal(1, session.EnvironmentAt(5).Darkness);
        Assert.Equal(0.30249, session.EnvironmentAt(28).Darkness, 5);
    }

    [Fact]
    public void Session_HasNoNightWithWeatherOff()
    {
        var region = new SeaRegion(30, "Black Sea", null, [new SeaProvince(8256, 3300, 1371)], [], 5632, 2048, 315);
        var location = new BattleLocation(region, region.Provinces[0], null, new GameDate(1936, 5, 6));
        var scenario = new BattleScenario("t", "ocean", "clear", 1, null,
            TestEntityFactory.Participant("a", [TestEntityFactory.Screen("a1")]), TestEntityFactory.Participant("d", [TestEntityFactory.Screen("d1")]),
            new Dictionary<string, PlaneEquipment>(), defines, new BattleRules { StartHourOfDay = 13, WeatherEnabled = false }, location: location);

        Assert.Equal(0, new BattleSession(scenario, 1).EnvironmentAt(10).Darkness);
    }

    [Theory]
    [InlineData("1936.5.6", 1936, 5, 6, 0)]
    [InlineData("1936.5.6.12", 1936, 5, 6, 12)]
    [InlineData("1941-12-07", 1941, 12, 7, 0)]
    public void GameDate_ReadsTheGamesFormats(string text, int year, int month, int day, int hour)
    {
        Assert.True(GameDate.TryParse(text, out var date));
        Assert.Equal(new GameDate(year, month, day, hour), date);
    }

    [Fact]
    public void GameDate_HasNoLeapDays()
    {
        Assert.Equal(new GameDate(1936, 3, 1, 1), new GameDate(1936, 2, 28, 23).AddHours(2));
        Assert.Equal(new GameDate(1937, 1, 1, 0), new GameDate(1936, 12, 31, 23).AddHours(1));
        Assert.Equal(59, new GameDate(1936, 3, 1).DayOfYear);
        Assert.False(GameDate.TryParse("1936.2.29", out _));
    }
}
