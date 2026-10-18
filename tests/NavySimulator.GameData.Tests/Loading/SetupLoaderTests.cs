using NavySimulator.Engine.Battles;

namespace NavySimulator.GameData.Tests.Loading;

public class SetupLoaderTests
{
    [Fact]
    public void LoadScenarioFromDirectory_LoadsRepositoryData()
    {
        var loader = new SetupLoader();

        var scenario = loader.LoadScenarioFromDirectory(RepositoryPaths.Data);

        Assert.NotEmpty(scenario.Attacker.Fleet.Ships);
        Assert.NotEmpty(scenario.Defender.Fleet.Ships);
        Assert.NotSame(scenario.Attacker.Fleet, scenario.Defender.Fleet);
    }

    [Fact]
    public void CatalogFiles_ListEveryFileTheCatalogIsReadFrom()
    {
        var catalog = new SetupLoader().ReadCatalog(RepositoryPaths.Data);
        IEnumerable<object> items = [.. catalog.Hulls, .. catalog.Modules, .. catalog.Mios, .. catalog.ShipDesigns, .. catalog.Researches, .. catalog.Spirits, .. catalog.Planes, .. catalog.Fleets];

        var files = SetupLoader.CatalogFiles(RepositoryPaths.Data);

        Assert.All(items, item => Assert.Contains(catalog.SourceFileOf(item), files));
        Assert.DoesNotContain("battle-scenario.json", files);
    }

    [Fact]
    public void LoadScenarioFromDirectory_AppliesExtraDefineSources()
    {
        var loader = new SetupLoader();
        var tfb = Path.Combine(RepositoryPaths.Data, "defines", "tfb-legacy.lua");

        var scenario = loader.LoadScenarioFromDirectory(RepositoryPaths.Data, extraDefineSources: [tfb]);

        Assert.Equal(0.02, scenario.Defines.NNavy.COMBAT_MIN_HIT_CHANCE);
        Assert.Contains(tfb, scenario.Defines.Sources);
    }

    [Fact]
    public void LoadedScenario_RunsInTheEngine()
    {
        var scenario = new SetupLoader().LoadScenarioFromDirectory(RepositoryPaths.Data);

        var result = new BattleEngine().Simulate(scenario, 1);

        Assert.True(result.HoursFought > 0);
        Assert.NotEmpty(result.Shots);
    }
}
