using NavySimulator.Engine.Defines;
using NavySimulator.GameData.Contracts;
using NavySimulator.Web.Services;

namespace NavySimulator.Web.Tests;

/// <summary>S-REGION, S-NIGHT, S-WEATHER, S-TERRAIN in the GUI: the location panel's summaries (imported TFB data).</summary>
public sealed class LocationInfoTests : IDisposable
{
    private readonly TestWorkspace test = new();

    private Workspace Workspace => test.Workspace;

    public void Dispose()
    {
        test.Dispose();
    }

    [Fact]
    public void Regions_AreListedByName()
    {
        var regions = LocationInfo.Regions(Workspace.Catalog);

        Assert.Contains((30, "Black Sea (30)"), regions);
        Assert.Equal(regions.OrderBy(region => region.Label, StringComparer.OrdinalIgnoreCase), regions);
    }

    [Fact]
    public void Summaries_DescribeTheBlackSeaInMay()
    {
        var blackSea = LocationInfo.Region(Workspace.Catalog, 30)!;
        var defines = GameDefines.Vanilla();

        Assert.Equal("1–31 May, 12 to 30 °C: no weather 59 %, light rain 29 %, heavy rain 12 %",
            LocationInfo.PeriodSummary(blackSea, "1936.5.6", defines)?.Replace(' ', ' '));
        // [LOG] Province 8256 on 6 May is dark 17:00 to 03:00 with twilight at 16:00; the battle reads it one hour late.
        Assert.StartsWith("dark 18:00 to 05:00, twilight 17:00 (33",
            LocationInfo.NightSummary(blackSea, 8256, "1936.5.6", defines)?.Replace(' ', ' '));
        Assert.Null(LocationInfo.NightSummary(blackSea, 8256, null, defines));
    }

    [Fact]
    public void Terrain_IsTheScenariosOrTheRegions()
    {
        var scenario = new BattleScenarioDto { RegionID = 30 };

        Assert.Equal("ocean", LocationInfo.Terrain(Workspace.Catalog, scenario)?.ID);
        scenario.Terrain = "water_fjords";
        var fjords = LocationInfo.Terrain(Workspace.Catalog, scenario)!;
        Assert.Contains("positioning −15 %", LocationInfo.TerrainSummary(fjords), StringComparison.Ordinal);
        Assert.Contains("battleship", LocationInfo.TerrainSummary(fjords), StringComparison.Ordinal);
        Assert.Contains("attack −20 %, defence −20 %, speed −20 %", LocationInfo.TerrainSummary(fjords), StringComparison.Ordinal);
        Assert.Null(LocationInfo.Terrain(Workspace.Catalog, new BattleScenarioDto()));
    }
}
